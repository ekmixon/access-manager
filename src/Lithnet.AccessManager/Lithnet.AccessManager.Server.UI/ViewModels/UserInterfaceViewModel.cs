﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Lithnet.AccessManager.Server.Configuration;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.IconPacks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Stylet;

namespace Lithnet.AccessManager.Server.UI
{
    public class UserInterfaceViewModel : Screen, IHelpLink
    {
        private readonly UserInterfaceOptions model;

        private readonly IDialogCoordinator dialogCoordinator;

        private readonly IAppPathProvider appPathProvider;

        private readonly ILogger<UserInterfaceViewModel> logger;

        public UserInterfaceViewModel(UserInterfaceOptions model, IDialogCoordinator dialogCoordinator, IAppPathProvider appPathProvider, INotifiableEventPublisher eventPublisher, ILogger<UserInterfaceViewModel> logger)
        {
            this.appPathProvider = appPathProvider;
            this.dialogCoordinator = dialogCoordinator;
            this.logger = logger;
            this.model = model;
            this.DisplayName = "User interface";
            model.PhoneticSettings ??= new PhoneticSettings();

            eventPublisher.Register(this);
        }

        public string HelpLink => Constants.HelpLinkPageUserInterface;

        protected override void OnInitialActivate()
        {
            this.LoadImage();
            this.BuildPreview();
        }

        [NotifiableProperty]
        public string Title { get => this.model.Title; set => this.model.Title = value; }

        [NotifiableProperty]
        public AuditReasonFieldState UserSuppliedReason { get => this.model.UserSuppliedReason; set => this.model.UserSuppliedReason = value; }

        public IEnumerable<AuditReasonFieldState> UserSuppliedReasonValues => Enum.GetValues(typeof(AuditReasonFieldState)).Cast<AuditReasonFieldState>();

        public BitmapImage Image { get; set; }

        public string ImageError { get; set; }

        [NotifiableProperty]
        public bool DisableTextToSpeech { get => this.model.PhoneticSettings.DisableTextToSpeech; set => this.model.PhoneticSettings.DisableTextToSpeech = value; }

        [NotifiableProperty]
        public bool HidePhoneticBreakdown { get => this.model.PhoneticSettings.HidePhoneticBreakdown; set => this.model.PhoneticSettings.HidePhoneticBreakdown = value; }

        [PropertyChanged.OnChangedMethod(nameof(BuildPreview))]
        [NotifiableProperty]
        public string UpperPrefix { get => this.model.PhoneticSettings.UpperPrefix; set => this.model.PhoneticSettings.UpperPrefix = value; }

        [PropertyChanged.OnChangedMethod(nameof(BuildPreview))]
        [NotifiableProperty]
        public string LowerPrefix { get => this.model.PhoneticSettings.LowerPrefix; set => this.model.PhoneticSettings.LowerPrefix = value; }

        [PropertyChanged.OnChangedMethod(nameof(BuildPreview))]
        [NotifiableProperty]
        public int GroupSize { get => this.model.PhoneticSettings.GroupSize; set => this.model.PhoneticSettings.GroupSize = value; }

        [PropertyChanged.OnChangedMethod(nameof(BuildPreview))]
        public string PreviewPassword { get; set; } = "Password123!";

        public string Preview { get; private set; }

        public void BuildPreview()
        {
            string previewPassword = this.PreviewPassword;

            if (string.IsNullOrWhiteSpace(previewPassword))
            {
                this.Preview = null;
                return;
            }

            PhoneticStringProvider p = new PhoneticStringProvider(this.model.PhoneticSettings);

            StringBuilder builder = new StringBuilder();

            foreach (var segment in p.GetPhoneticTextSections(previewPassword))
            {
                builder.AppendLine(segment);
            }

            this.Preview = builder.ToString();
        }

        private void LoadImage()
        {
            try
            {
                string path = $"{this.appPathProvider.ImagesPath}\\logo.png";
                this.Image = this.LoadImageFromFile(path);
            }
            catch (Exception ex)
            {
                this.logger.LogError(EventIDs.UIGenericError, ex, "Could not load logo");
                this.ImageError = $"There was an error loading the logo image\r\n{ex.Message}";
            }
        }

        private BitmapImage LoadImageFromFile(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            return bitmap;
        }

        private void Replace(BitmapImage image)
        {
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

            using (var fileStream = new System.IO.FileStream($"{this.appPathProvider.ImagesPath}\\logo.png", System.IO.FileMode.Create))
            {
                encoder.Save(fileStream);
            }
        }

        public async Task SelectImage()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.DereferenceLinks = true;
            openFileDialog.Filter = @"Image Files|*.JPG;*.JPEG*.jpg;*.jpeg;*.PNG;*.png;|PNG|*.PNG;*.png|JPEG|*.JPG;*.JPEG*.jpg;*.jpeg";

            openFileDialog.Multiselect = false;

            var oldImage = this.Image;

            if (openFileDialog.ShowDialog(Window.GetWindow(this.View)) == true)
            {
                try
                {
                    this.Image = this.LoadImageFromFile(openFileDialog.FileName);
                    this.Replace(this.Image);
                    this.ImageError = null;
                }
                catch (Exception ex)
                {
                    this.logger.LogError(EventIDs.UIGenericError, ex, "Could not replace image");
                    this.Image = oldImage;
                    this.ImageError = null;
                    await this.dialogCoordinator.ShowMessageAsync(this, "Cannot open image", $"There was an error replacing the image\r\n{ex.Message}");
                }
            }
        }


        public PackIconMaterialKind Icon => PackIconMaterialKind.Application;

    }
}
