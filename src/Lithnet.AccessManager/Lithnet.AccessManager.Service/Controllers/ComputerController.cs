﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using Lithnet.AccessManager.Server.Auditing;
using Lithnet.AccessManager.Server.Authorization;
using Lithnet.AccessManager.Server.Configuration;
using Lithnet.AccessManager.Server.Exceptions;
using Lithnet.AccessManager.Server.Extensions;
using Lithnet.AccessManager.Service.App_LocalResources;
using Lithnet.AccessManager.Service.AppSettings;
using Lithnet.AccessManager.Service.Internal;
using Lithnet.AccessManager.Service.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IAuthorizationService = Lithnet.AccessManager.Server.Authorization.IAuthorizationService;

namespace Lithnet.AccessManager.Service.Controllers
{
    [Authorize(Policy = "RequireAuthorizedUser")]
    [Localizable(true)]
    public class ComputerController : Controller
    {
        private readonly IAuthenticationProvider authenticationProvider;
        private readonly IAuthorizationService authorizationService;
        private readonly IDirectory directory;
        private readonly IJitAccessProvider jitAccessProvider;
        private readonly ILogger logger;
        private readonly IPasswordProvider passwordProvider;
        private readonly IRateLimiter rateLimiter;
        private readonly IAuditEventProcessor reporting;
        private readonly UserInterfaceOptions userInterfaceSettings;
        private readonly IBitLockerRecoveryPasswordProvider bitLockerProvider;

        public ComputerController(IAuthorizationService authorizationService, ILogger<ComputerController> logger, IDirectory directory,
            IAuditEventProcessor reporting, IRateLimiter rateLimiter, IOptionsSnapshot<UserInterfaceOptions> userInterfaceSettings, IAuthenticationProvider authenticationProvider, IPasswordProvider passwordProvider, IJitAccessProvider jitAccessProvider, IBitLockerRecoveryPasswordProvider bitLockerProvider)
        {
            this.authorizationService = authorizationService;
            this.logger = logger;
            this.directory = directory;
            this.reporting = reporting;
            this.rateLimiter = rateLimiter;
            this.userInterfaceSettings = userInterfaceSettings.Value;
            this.authenticationProvider = authenticationProvider;
            this.passwordProvider = passwordProvider;
            this.jitAccessProvider = jitAccessProvider;
            this.bitLockerProvider = bitLockerProvider;
        }

        public IActionResult AccessRequest()
        {
            if (!TryGetUser(out _, out IActionResult actionResult))
            {
                return actionResult;
            }

            return this.View(new AccessRequestModel
            {
                ShowReason = this.userInterfaceSettings.UserSuppliedReason != AuditReasonFieldState.Hidden,
                ReasonRequired = this.userInterfaceSettings.UserSuppliedReason == AuditReasonFieldState.Required,
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AccessRequestType(AccessRequestModel model)
        {
            model.ShowReason = this.userInterfaceSettings.UserSuppliedReason != AuditReasonFieldState.Hidden;
            model.ReasonRequired = this.userInterfaceSettings.UserSuppliedReason == AuditReasonFieldState.Required;

            if (!this.ModelState.IsValid)
            {
                return this.View("AccessRequest", model);
            }

            //Thread.Sleep(10000);

            IUser user = null;
            IComputer computer = null;
            model.FailureReason = null;

            try
            {
                if (!TryGetUser(out user, out IActionResult actionResult))
                {
                    return actionResult;
                }

                this.logger.LogEventSuccess(EventIDs.UserRequestedAccessToComputer, string.Format(LogMessages.UserHasRequestedAccessToComputer, user.MsDsPrincipalName, model.ComputerName));

                if (!ValidateRequestReason(model, user, out actionResult))
                {
                    return actionResult;
                }

                if (!TryGetComputer(model, user, out computer, out actionResult))
                {
                    return actionResult;
                }

                return GetPreAuthorizationResponse(model, user, computer);
            }
            catch (Exception ex)
            {
                this.logger.LogEventError(EventIDs.UnexpectedError, string.Format(LogMessages.UnhandledError, computer?.MsDsPrincipalName, user?.MsDsPrincipalName), ex);

                return this.View("AccessRequestError", new ErrorModel
                {
                    Heading = UIMessages.UnableToProcessRequest,
                    Message = UIMessages.UnexpectedError
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult AccessResponse(AccessRequestModel model)
        {
            model.ShowReason = this.userInterfaceSettings.UserSuppliedReason != AuditReasonFieldState.Hidden;
            model.ReasonRequired = this.userInterfaceSettings.UserSuppliedReason == AuditReasonFieldState.Required;
            model.RequestType = model.RequestType == 0 ? AccessMask.LocalAdminPassword : model.RequestType;

            if (!this.ModelState.IsValid)
            {
                return this.View("AccessRequest", model);
            }

            IUser user = null;
            IComputer computer = null;
            model.FailureReason = null;

            try
            {
                if (!TryGetUser(out user, out IActionResult actionResult))
                {
                    return actionResult;
                }

                this.logger.LogEventSuccess(EventIDs.UserRequestedAccessToComputer, string.Format(LogMessages.UserHasRequestedAccessToComputer, user.MsDsPrincipalName, model.ComputerName));

                if (!ValidateRateLimit(model, user, out actionResult))
                {
                    return actionResult;
                }

                if (!ValidateRequestReason(model, user, out actionResult))
                {
                    return actionResult;
                }

                if (!TryGetComputer(model, user, out computer, out actionResult))
                {
                    return actionResult;
                }

                return GetAuthorizationResponse(model, user, computer);
            }
            catch (Exception ex)
            {
                this.logger.LogEventError(EventIDs.UnexpectedError, string.Format(LogMessages.UnhandledError, computer?.MsDsPrincipalName, user?.MsDsPrincipalName), ex);

                return this.View("AccessRequestError", new ErrorModel
                {
                    Heading = UIMessages.UnableToProcessRequest,
                    Message = UIMessages.UnexpectedError
                });
            }
        }

        private void AuditAuthZFailure(AccessRequestModel model, AuthorizationResponse authorizationResponse, IUser user, IComputer computer)
        {
            AuditableAction action = new AuditableAction
            {
                AuthzResponse = authorizationResponse,
                User = user,
                Computer = computer,
                IsSuccess = false,
                RequestedComputerName = model.ComputerName,
                RequestReason = model.UserRequestReason,
                Message = string.Format(LogMessages.AuthorizationFailed, user.MsDsPrincipalName, model.ComputerName),
                EventID = authorizationResponse.Code switch
                {
                    AuthorizationResponseCode.NoMatchingRuleForComputer => EventIDs.AuthZFailedNoTargetMatch,
                    AuthorizationResponseCode.NoMatchingRuleForUser => EventIDs.AuthZFailedNoReaderPrincipalMatch,
                    AuthorizationResponseCode.ExplicitlyDenied => EventIDs.AuthZExplicitlyDenied,
                    _ => EventIDs.AuthZFailed,
                }
            };

            this.reporting.GenerateAuditEvent(action);
        }

        private IActionResult GetAuthorizationResponse(AccessRequestModel model, IUser user, IComputer computer)
        {
            try
            {
                // Do authorization check first.

                AuthorizationResponse authResponse = this.authorizationService.GetAuthorizationResponse(user, computer, model.RequestType);

                if (!authResponse.IsAuthorized())
                {
                    this.AuditAuthZFailure(model, authResponse, user, computer);
                    model.FailureReason = UIMessages.NotAuthorized;
                    return this.View("AccessRequest", model);
                }

                // Do actual work only if authorized.
                if (authResponse.EvaluatedAccess == AccessMask.LocalAdminPassword)
                {
                    return this.GetLapsPassword(model, user, computer, (LapsAuthorizationResponse)authResponse);
                }
                else if (authResponse.EvaluatedAccess == AccessMask.LocalAdminPasswordHistory)
                {
                    return this.GetLapsPasswordHistory(model, user, computer, (LapsHistoryAuthorizationResponse)authResponse);
                }
                else if (authResponse.EvaluatedAccess == AccessMask.Jit)
                {
                    return this.GrantJitAccess(model, user, computer, (JitAuthorizationResponse)authResponse);
                }
                else if (authResponse.EvaluatedAccess == AccessMask.BitLocker)
                {
                    return this.GetBitLockerRecoveryPasswords(model, user, computer, (BitLockerAuthorizationResponse) authResponse);
                }
                else
                {
                    throw new AccessManagerException(@"The evaluated access response mask was not supported");
                }
            }
            catch (AuditLogFailureException ex)
            {
                this.logger.LogEventError(EventIDs.AuthZFailedAuditError, string.Format(LogMessages.AuthZFailedAuditError, user.MsDsPrincipalName, model.ComputerName), ex);

                return this.View("AccessRequestError", new ErrorModel
                {
                    Heading = UIMessages.AccessDenied,
                    Message = UIMessages.AuthZFailedAuditError
                });
            }
            catch (Exception ex)
            {
                this.logger.LogEventError(EventIDs.AuthZError, string.Format(LogMessages.AuthZError, user.MsDsPrincipalName, computer.MsDsPrincipalName), ex);

                return this.View("AccessRequestError", new ErrorModel
                {
                    Heading = UIMessages.UnableToProcessRequest,
                    Message = UIMessages.AuthZError
                });
            }
        }
        private IActionResult GetBitLockerRecoveryPasswords(AccessRequestModel model, IUser user, IComputer computer, BitLockerAuthorizationResponse authResponse)
        {
            try
            {
                IList<BitLockerRecoveryPassword> entries = this.bitLockerProvider.GetBitLockerRecoveryPasswords(computer);

                if (entries == null || entries.Count == 0)
                {
                    throw new NoPasswordException();
                }

                this.reporting.GenerateAuditEvent(new AuditableAction
                {
                    AuthzResponse = authResponse,
                    RequestedComputerName = model.ComputerName,
                    RequestReason = model.UserRequestReason,
                    IsSuccess = true,
                    User = user,
                    Computer = computer,
                    EventID = EventIDs.ComputerBitLockerAccessGranted,
                });

                return this.View("AccessResponseBitLocker", new BitLockerRecoveryPasswordsModel()
                {
                    ComputerName = computer.MsDsPrincipalName,
                    Passwords = entries
                });
            }
            catch (NoPasswordException)
            {
                this.logger.LogEventError(EventIDs.BitLockerKeysNotPresent, string.Format(LogMessages.BitLockerKeysNotPresent, computer.MsDsPrincipalName, user.MsDsPrincipalName));

                model.FailureReason = UIMessages.BitLockerKeysNotPresent;

                return this.View("AccessResponseNoBitLocker", new NoPasswordModel
                {
                    Heading = UIMessages.HeadingBitLockerKeys,
                    Message = UIMessages.BitLockerKeysNotPresent,
                    ComputerName = computer.MsDsPrincipalName
                });
            }
            catch (Exception ex)
            {
                this.logger.LogEventError(EventIDs.BitLockerKeyAccessError, string.Format(LogMessages.BitLockerKeyAccessError, computer.MsDsPrincipalName, user.MsDsPrincipalName), ex);

                return this.View("AccessRequestError", new ErrorModel
                {
                    Heading = UIMessages.UnableToProcessRequest,
                    Message = UIMessages.BitLockerKeyAccessError
                });
            }
        }

        private IActionResult GetLapsPassword(AccessRequestModel model, IUser user, IComputer computer, LapsAuthorizationResponse authResponse)
        {
            try
            {
                DateTime? newExpiry = authResponse.ExpireAfter.Ticks > 0 ? DateTime.UtcNow.Add(authResponse.ExpireAfter) : (DateTime?)null;

                PasswordEntry current = this.passwordProvider.GetCurrentPassword(computer, newExpiry, authResponse.RetrievalLocation);

                if (current == null)
                {
                    throw new NoPasswordException();
                }

                this.reporting.GenerateAuditEvent(new AuditableAction
                {
                    AuthzResponse = authResponse,
                    RequestedComputerName = model.ComputerName,
                    RequestReason = model.UserRequestReason,
                    IsSuccess = true,
                    User = user,
                    Computer = computer,
                    EventID = EventIDs.ComputerPasswordActiveAccessGranted,
                    AccessExpiryDate = current.ExpiryDate?.ToLocalTime().ToString(CultureInfo.CurrentUICulture)
                });

                return this.View("AccessResponseCurrentPassword", new CurrentPasswordModel()
                {
                    ComputerName = computer.MsDsPrincipalName,
                    Password = current.Password,
                    ValidUntil = current.ExpiryDate?.ToLocalTime(),
                });
            }
            catch (NoPasswordException)
            {
                this.logger.LogEventError(EventIDs.LapsPasswordNotPresent, string.Format(LogMessages.NoLapsPassword, computer.MsDsPrincipalName, user.MsDsPrincipalName));

                model.FailureReason = UIMessages.NoLapsPassword;

                return this.View("AccessResponseNoPasswords", new NoPasswordModel
                {
                    Heading = UIMessages.HeadingPasswordDetails,
                    Message = UIMessages.NoLapsPassword,
                    ComputerName = computer.MsDsPrincipalName
                });
            }
            catch (Exception ex)
            {
                this.logger.LogEventError(EventIDs.LapsPasswordError, string.Format(LogMessages.LapsPasswordError, computer.MsDsPrincipalName, user.MsDsPrincipalName), ex);

                return this.View("AccessRequestError", new ErrorModel
                {
                    Heading = UIMessages.UnableToProcessRequest,
                    Message = UIMessages.LapsPasswordError
                });
            }
        }

        private IActionResult GetLapsPasswordHistory(AccessRequestModel model, IUser user, IComputer computer, LapsHistoryAuthorizationResponse authResponse)
        {
            try
            {
                IList<PasswordEntry> history;

                try
                {
                    history = this.passwordProvider.GetPasswordHistory(computer);

                    if (history == null)
                    {
                        throw new NoPasswordException();
                    }
                }
                catch (NoPasswordException)
                {
                    this.logger.LogEventError(EventIDs.NoLapsPasswordHistory, string.Format(LogMessages.NoLapsPasswordHistory, computer.MsDsPrincipalName, user.MsDsPrincipalName));

                    return this.View("AccessResponseNoPasswords", new NoPasswordModel
                    {
                        Heading = UIMessages.HeadingPasswordDetails,
                        Message = UIMessages.NoLapsPasswordHistory,
                        ComputerName = computer.MsDsPrincipalName
                    });
                }

                this.reporting.GenerateAuditEvent(new AuditableAction
                {
                    AuthzResponse = authResponse,
                    RequestedComputerName = model.ComputerName,
                    RequestReason = model.UserRequestReason,
                    IsSuccess = true,
                    User = user,
                    Computer = computer,
                    EventID = EventIDs.ComputerPasswordHistoryAccessGranted
                });

                return this.View("AccessResponsePasswordHistory", new PasswordHistoryModel
                {
                    ComputerName = computer.MsDsPrincipalName,
                    PasswordHistory = history
                });
            }
            catch (Exception ex)
            {
                this.logger.LogEventError(EventIDs.LapsPasswordHistoryError, string.Format(LogMessages.LapsPasswordHistoryError, computer.MsDsPrincipalName, user.MsDsPrincipalName), ex);

                return this.View("AccessRequestError", new ErrorModel
                {
                    Heading = UIMessages.UnableToProcessRequest,
                    Message = UIMessages.LapsPasswordHistoryError
                });
            }
        }

        private IActionResult GetPreAuthorizationResponse(AccessRequestModel model, IUser user, IComputer computer)
        {
            try
            {
                AuthorizationResponse authResponse = this.authorizationService.GetPreAuthorization(user, computer);

                if (!authResponse.IsAuthorized())
                {
                    this.AuditAuthZFailure(model, authResponse, user, computer);

                    return this.View("AccessRequestError", new ErrorModel
                    {
                        Heading = UIMessages.AccessDenied,
                        Message = UIMessages.NotAuthorized
                    });
                }

                model.AllowedRequestTypes = authResponse.EvaluatedAccess;

                if (model.AllowedRequestTypes.HasFlag(AccessMask.LocalAdminPassword))
                {
                    model.RequestType = AccessMask.LocalAdminPassword;
                }
                else if (model.AllowedRequestTypes.HasFlag(AccessMask.LocalAdminPasswordHistory))
                {
                    model.RequestType = AccessMask.LocalAdminPasswordHistory;
                }
                else
                {
                    model.RequestType = AccessMask.Jit;
                }

                model.ComputerName = computer.MsDsPrincipalName;

                return this.View("AccessRequestType", model);
            }
            catch (Exception ex)
            {
                this.logger.LogEventError(EventIDs.PreAuthZError, string.Format(LogMessages.PreAuthZError, user.MsDsPrincipalName, computer.MsDsPrincipalName), ex);

                return this.View("AccessRequestError", new ErrorModel
                {
                    Heading = UIMessages.UnableToProcessRequest,
                    Message = UIMessages.PreAuthZError
                });
            }
        }

        private IActionResult GrantJitAccess(AccessRequestModel model, IUser user, IComputer computer, JitAuthorizationResponse authResponse)
        {
            Action undo = null;

            try
            {
                TimeSpan grantedAccessLength = this.jitAccessProvider.GrantJitAccess(this.directory.GetGroup(authResponse.AuthorizingGroup), user, computer, authResponse.AllowExtension, authResponse.ExpireAfter, out undo);

                DateTime expiryDate = DateTime.Now.Add(grantedAccessLength);

                this.reporting.GenerateAuditEvent(new AuditableAction
                {
                    AuthzResponse = authResponse,
                    RequestedComputerName = model.ComputerName,
                    RequestReason = model.UserRequestReason,
                    IsSuccess = true,
                    User = user,
                    Computer = computer,
                    EventID = EventIDs.ComputerJitAccessGranted,
                    AccessExpiryDate = expiryDate.ToString(CultureInfo.CurrentCulture)
                });

                var jitDetails = new JitDetailsModel(computer.MsDsPrincipalName, user.MsDsPrincipalName, expiryDate);

                return this.View("AccessResponseJit", jitDetails);
            }
            catch (Exception ex)
            {
                if (undo != null)
                {
                    this.logger.LogEventWarning(EventIDs.JitRollbackInProgress, LogMessages.JitRollbackInProgress, ex);

                    try
                    {
                        undo();
                    }
                    catch (Exception ex2)
                    {
                        this.logger.LogEventError(EventIDs.JitRollbackFailed, LogMessages.JitRollbackFailed, ex2);
                    }
                }

                this.logger.LogEventError(EventIDs.JitError, string.Format(LogMessages.JitError, computer.MsDsPrincipalName, user.MsDsPrincipalName), ex);

                ErrorModel errorModel = new ErrorModel
                {
                    Heading = UIMessages.UnableToGrantAccess,
                    Message = UIMessages.JitError
                };

                return this.View("AccessRequestError", errorModel);
            }
        }

        private void LogRateLimitEvent(AccessRequestModel model, IUser user, RateLimitResult rateLimitResult)
        {
            AuditableAction action = new AuditableAction
            {
                User = user,
                IsSuccess = false,
                RequestedComputerName = model.ComputerName,
                RequestReason = model.UserRequestReason,
            };

            if (rateLimitResult.IsUserRateLimit)
            {
                action.EventID = EventIDs.RateLimitExceededUser;
                action.Message = string.Format(LogMessages.RateLimitExceededUser, user.MsDsPrincipalName, rateLimitResult.IPAddress, rateLimitResult.Threshold, rateLimitResult.Duration);
            }
            else
            {
                action.EventID = EventIDs.RateLimitExceededIP;
                action.Message = string.Format(LogMessages.RateLimitExceededIP, user.MsDsPrincipalName, rateLimitResult.IPAddress, rateLimitResult.Threshold, rateLimitResult.Duration);
            }

            this.reporting.GenerateAuditEvent(action);
        }

        private bool TryGetComputer(AccessRequestModel model, IUser user, out IComputer computer, out IActionResult failure)
        {
            computer = null;
            failure = null;

            try
            {
                computer = this.directory.GetComputer(model.ComputerName.Trim()) ?? throw new ObjectNotFoundException();
                return true;
            }
            catch (AmbiguousNameException ex)
            {
                this.logger.LogEventError(EventIDs.ComputerNameAmbiguous, string.Format(LogMessages.ComputerNameAmbiguous, user.MsDsPrincipalName, model.ComputerName), ex);

                model.FailureReason = UIMessages.ComputerNameAmbiguous;
                failure = this.View("AccessRequest", model);
            }
            catch (ObjectNotFoundException ex)
            {
                this.logger.LogEventError(EventIDs.ComputerNotFoundInDirectory, string.Format(LogMessages.ComputerNotFoundInDirectory, user.MsDsPrincipalName, model.ComputerName), ex);

                model.FailureReason = UIMessages.ComputerNotFoundInDirectory;
                failure = this.View("AccessRequest", model);
            }
            catch (Exception ex)
            {
                this.logger.LogEventError(EventIDs.ComputerDiscoveryError, string.Format(LogMessages.ComputerDiscoveryError, user.MsDsPrincipalName, model.ComputerName), ex);

                model.FailureReason = UIMessages.ComputerDiscoveryError;
                failure = this.View("AccessRequest", model);
            }

            return false;
        }

        private bool TryGetUser(out IUser user, out IActionResult failure)
        {
            failure = null;

            try
            {
                user = this.authenticationProvider.GetLoggedInUser() ?? throw new ObjectNotFoundException();
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogEventError(EventIDs.IdentityDiscoveryError, LogMessages.IdentityDiscoveryError, ex);
                user = null;

                ErrorModel model = new ErrorModel
                {
                    Heading = UIMessages.AccessDenied,
                    Message = UIMessages.IdentityDiscoveryError,
                };

                failure = this.View("AccessRequestError", model);
                return false;
            }
        }

        private bool ValidateRateLimit(AccessRequestModel model, IUser user, out IActionResult view)
        {
            view = null;

            var rateLimitResult = this.rateLimiter.GetRateLimitResult(user.Sid.ToString(), this.Request);

            if (rateLimitResult.IsRateLimitExceeded)
            {
                this.LogRateLimitEvent(model, user, rateLimitResult);
                var errorModel = new ErrorModel
                {
                    Message = UIMessages.RateLimitError,
                    Heading = "Too many requests"
                };

                view = this.View("AccessRequestError", errorModel);
                return false;
            }

            return true;
        }

        private bool ValidateRequestReason(AccessRequestModel model, IUser user, out IActionResult actionResult)
        {
            actionResult = null;

            if (string.IsNullOrWhiteSpace(model.UserRequestReason) && this.userInterfaceSettings.UserSuppliedReason == AuditReasonFieldState.Required)
            {
                logger.LogEventError(EventIDs.ReasonRequired, string.Format(LogMessages.ReasonRequired, user.MsDsPrincipalName));
                model.FailureReason = UIMessages.ReasonRequired;
                actionResult = this.View("AccessRequest", model);
                return false;
            }

            return true;
        }
    }
}