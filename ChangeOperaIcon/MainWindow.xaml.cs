using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ChangeOperaIcon
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {
        #region Champs

        private string[] OperaVersions; //Liste des dossiers présents dans OperaPath
        private string OperaPath; //Dossier d'Opera
        private readonly string AppPath; //Dossier de l'application
        private string CustomIcon;
        private readonly bool AppelDeCRB = true; //Utilisé pour vérifier si l'utilisateur a bien cliqué sur "customradiobutton"
        private bool AppelDeOCB = true; //Utilisé pour vérifier si l'utilisateur a bien cliqué sur "originalicon"
        private bool chk32; //Utilisé pour savoir si _32radiobutton est coché
        private bool chkoriginal; //Utilisé pour savoir si originalicon est coché

        private int da; //Est défini par la valeur de "DefineArguments()"

        private Settings sgs = new Settings("settings.ini");

        private struct OperaSettings //Paramètres présents dans "settings.ini"
        {
            public string OperaMode, OperaPath;
        }

        private OperaSettings osgs;
        private readonly Process pc = new Process();
        private readonly ProcessStartInfo p = new ProcessStartInfo();

        #endregion

        public MainWindow()
        {
            InitializeComponent();

            AppPath = AppDomain.CurrentDomain.BaseDirectory; //Définition du dossier de l'application

            RefreshSettings(); //Chargement des paramètres

            var b = new Bitmap(typeof(MainWindow), "OperaIcons.opera.png");
            iconpreview.Source = BitmapToBitmapSource(b);

            #region Vérification administrateur

            if (IsAdministrator()) //On vérifie que l'application n'est pas lancée en tant qu'administrateur
            {
                if (MessageBox.Show("Il n'est pas nécessaire d'exécuter l'application en tant qu'administrateur\nAu contraire, cela peur provoquer des bugs\n\nVoulez-vous fermer l'application ?", "Attention !", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes) == MessageBoxResult.Yes)
                {
                    Close();
                }
            }

            #endregion

            #region Localisation Opera

            bool f = false; //Utilisé pour vérifier si Opera a été localisé dans les dossiers ProgramFiles ou ProgramFiles(x86)

            if (Directory.Exists(Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%") + "\\Opera")) //Chercher Opera dans ProgramFiles(x86)
            {
                _32radiobutton.IsEnabled = true;
                _32radiobutton.IsChecked = true;
                _64radiobutton.IsEnabled = false;

                f = true; //On indique qu'Opera a été localisé
            }

            if (Directory.Exists(Environment.ExpandEnvironmentVariables("%ProgramW6432%") + "\\Opera")) //Chercher Opera dans ProgramFiles
            {

                _64radiobutton.IsEnabled = true;
                _64radiobutton.IsChecked = true;

                f = true; //On indique qu'Opera a été localisé
            }

            sgs.CanWrite = true;

            if (osgs.OperaMode != "Custom" || osgs.OperaMode == "Custom" && osgs.OperaPath == "")
            {
                if (!f) //Si Opera n'a pas été localisé
                {
                    AppelDeCRB = false;

                    _64radiobutton.IsEnabled = false;
                    customradiobutton.IsChecked = true;

                    if (MessageBox.Show("Nous n'avons pas pu localiser Opera\nVoulez-vous choisir son emplacement ?", "", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                    {
                        if (!AskOperaPath())
                        {
                            Close();
                        }
                    }
                    else
                    {
                        Close();
                    }

                    AppelDeCRB = true;
                }
            }
            else
            {
                customradiobutton.IsChecked = true;
            }

            #region Application des paramètres

            if (osgs.OperaMode == "32" && _32radiobutton.IsEnabled)
            {
                _32radiobutton.IsChecked = true;
            }
            else if (osgs.OperaMode == "64" && _64radiobutton.IsEnabled)
            {
                _64radiobutton.IsChecked = true;
            }
            else if (osgs.OperaMode == "Custom" && osgs.OperaPath != "")
            {
                AppelDeCRB = false;
                customradiobutton.IsChecked = true;
                OperaPath = osgs.OperaPath;
                AppelDeCRB = true;
            }
            else //Si les paramètres de "settings.ini" sont incorrects
            {
                if (_32radiobutton.IsChecked == true)
                    sgs.WriteSettings("OperaMode", "32");
                else if (_64radiobutton.IsChecked == true)
                    sgs.WriteSettings("OperaMode", "64");
                else
                    sgs.WriteSettings("OperaMode", "Custom");
            }

            #endregion

            RefreshVersions(); //Remplissage de la liste des versions

            #endregion
        }

        private void RefreshSettings()
        {
            sgs.RefreshSettings();

            osgs.OperaMode = sgs.GetSetting("OperaMode");
            osgs.OperaPath = sgs.GetSetting("OperaPath");

            if (osgs.OperaPath != "")
            {
                delbutton.ToolTip = "Supprimer la personnalisation (" + osgs.OperaPath + ")";
                delbutton.IsEnabled = true;
            }
        }

        #region Méthodes

        private static BitmapSource BitmapToBitmapSource(Bitmap bitmap) => System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

        private static bool EstUneVersion(string ChaineAVerifier)
        {
            bool Result;
            Result = true;
            foreach (char Caractere in ChaineAVerifier)
            {
                if (!char.IsDigit(Caractere) && Caractere != '.')
                {
                    Result = false;
                }
            }

            return Result;
        }

        private static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static bool ExtractEmbeddedResource(string outputDir, string resourceLocation, string file)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceLocation + @"." + file))
            {
                try
                {
                    using (var fileStream = new FileStream(System.IO.Path.Combine(outputDir, file), FileMode.Create))
                    {
                        for (int i = 0; i < stream.Length; i++)
                        {
                            fileStream.WriteByte((byte)stream.ReadByte());
                        }
                        fileStream.Close();
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("L’opération demandée n’a pu s’accomplir sur un fichier ayant une section mappée utilisateur ouverte."))
                    {
                        MessageBox.Show("L'accès au fichier \"" + outputDir + file + "\" est refusé.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        MessageBox.Show(ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    return false;
                }
            }
        }

        private static bool ExtractEmbeddedResources(string outputDir, string resourceLocation, List<string> files)
        {
            bool ok = true;

            foreach (string file in files)
            {
                try
                {
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceLocation + @"." + file))
                    {
                        using (var fileStream = new FileStream(System.IO.Path.Combine(outputDir, file), FileMode.Create))
                        {
                            for (int i = 0; i < stream.Length; i++)
                            {
                                fileStream.WriteByte((byte)stream.ReadByte());
                            }
                            fileStream.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("L’opération demandée n’a pu s’accomplir sur un fichier ayant une section mappée utilisateur ouverte."))
                    {
                        MessageBox.Show("L'accès au fichier \"" + outputDir + file + "\" est refusé.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        MessageBox.Show(ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    ok = false;
                }
            }

            return ok;
        }

        private void StartRH()
        {
            bool exc = false; //Utilisé pour vérifier si l'opération a été annulée

            if (!File.Exists("ResHack\\ResourceHacker.exe")) //Création de Resource Hacker s'il n'est pas dans le dossier
            {
                var files = new List<string>
                {
                    "ResourceHacker.exe",
                    "ResourceHacker.def",
                    "ResourceHacker.ini"
                };

                Directory.CreateDirectory("ResHack");

                ExtractEmbeddedResources(AppPath + "\\ResHack", "ChangeOperaIcon.ResHack", files);
            }

            try
            {
                pc.Start(); //Démarrage de ResourceHacker
                pc.WaitForExit();

                switch (da) //Suppression des fichiers .ico
                {
                    case 2:
                        {
                            File.Delete("original.ico");
                            break;
                        }
                    case 3:
                        {
                            File.Delete("opera.ico");
                            break;
                        }
                }

                if (!exc) //Si l'opération n'a pas été annulée
                {
                    string log = File.ReadAllText("ResHack\\ResourceHacker.log"); //Lecture du log
                    log = log.Replace("\0", "");

                    if (log.Contains("Success!")) //Messages de fin
                    {
                        ResetIconCache();
                        MessageBox.Show("Icône modifiée avec succès !", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("L'icône n'a pas été changée\nVouci le fichier log :\n\n" + log, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

            }
            catch (Exception ex)
            {
                switch (ex.Message)
                {
                    case "L’opération a été annulée par l’utilisateur":
                        {
                            MessageBox.Show(ex.Message, "Annulation", MessageBoxButton.OK, MessageBoxImage.Stop);
                            break;
                        }

                    case "Le fichier spécifié est introuvable":
                        {
                            MessageBox.Show("Impossible de démarrer ResourceHacker.\nIl n'est pas dans le dossier \"ResHack\"", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                        }
                    default:
                        {
                            MessageBox.Show(ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                        }
                }

                switch (da) //Suppression des fichiers .ico
                {
                    case 2:
                        {
                            File.Delete("original.ico");
                            break;
                        }
                    case 3:
                        {
                            File.Delete("opera.ico");
                            break;
                        }
                }

                exc = true;
            }
        }

        private int DefineArgs(string path)
        {
            if (customicon.IsChecked == true)
            {
                p.FileName = "ResHack\\ResourceHacker.exe";
                p.Arguments = "-open \"" + path + "\" -save \"" + path + "\" -action addoverwrite -res \"" + CustomIcon + "\"-mask ICONGROUP,1,";
                p.Verb = "runas";
                return 1;
            }
            else if (originalicon.IsChecked == true)
            {
                ExtractEmbeddedResource(AppPath, "ChangeOperaIcon.OperaIcons", "original.ico");
                p.FileName = "ResHack\\ResourceHacker.exe";
                p.Arguments = "-open \"" + path + "\" -save \"" + path + "\" -action addoverwrite -res original.ico -mask ICONGROUP,1,";
                p.Verb = "runas";
                return 2;
            }
            else
            {
                ExtractEmbeddedResource(AppPath, "ChangeOperaIcon.OperaIcons", "opera.ico");
                p.FileName = "ResHack\\ResourceHacker.exe";
                p.Arguments = "-open \"" + path + "\" -save \"" + path + "\" -action addoverwrite -res opera.ico -mask ICONGROUP,1,";
                p.Verb = "runas";
                return 3;
            }
        }

        private void ResetIconCache()
        {
            ExtractEmbeddedResource(AppPath, "ChangeOperaIcon", "ie4uinit.exe");

            var p2 = new ProcessStartInfo("ie4uinit.exe", "-show");

            var pc2 = new Process() { StartInfo = p2 };
            pc2.Start();
            pc2.WaitForExit();

            File.Delete("ie4uinit.exe");
        }

        private void RefreshVersions()
        {
            if (OperaVersions != null)
                OperaVersions = new string[0]; //On vide le tableau OperaVersions
            if (ListeDesVersions != null)
                ListeDesVersions.Items.Clear(); //On vide la liste ListeDesVersions

            if (Directory.Exists(OperaPath))
                OperaVersions = Directory.GetDirectories(OperaPath); //Lister les dossiers de Opera dans ProgramFiles dans le tableau OperaVersions

            if (OperaVersions != null)
            {
                for (int i = 0; i < OperaVersions.Length; i++) //Pour chaque dossier dans .\Opera\
                {
                    if (EstUneVersion(OperaVersions[i].Substring(OperaVersions[i].LastIndexOf('\\') + 1))) //S'il a la forme d'une version
                    {
                        ListeDesVersions.Items.Add(new ListBoxItem { Content = OperaVersions[i].Substring(OperaVersions[i].LastIndexOf('\\') + 1) }); //L'ajouter dans la liste

                        /*#region Trier
                        CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(ListeDesVersions.Items);
                        view.SortDescriptions.Clear();
                        view.SortDescriptions.Add(new SortDescription("", ListSortDirection.Descending));
                        #endregion*/
                    }
                }
            }
        }

        private bool AskOperaPath()
        {
            bool b = false;

            using (var fd = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (fd.ShowDialog() == System.Windows.Forms.DialogResult.OK) //Si un dossier est sélectionné
                {
                    OperaPath = fd.SelectedPath; //Définition de OperaPath

                    if (MessageBox.Show("Voulez-vous mémoriser cet emplacement ?", "", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) //Mémorisation de l'emplacement
                    {
                        sgs.WriteSettings("OperaPath", OperaPath);
                        sgs.WriteSettings("OperaMode", "Custom");

                        RefreshSettings();

                        delbutton.IsEnabled = true;
                    }

                    RefreshVersions();

                    b = true;
                }
            }

            return b;
        }

        #endregion

        #region Events

        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

            string versioncliquée = (sender as ListBoxItem).Content.ToString();
            string cheminversion = OperaPath + "\\" + versioncliquée + "\\";

            if (File.Exists(cheminversion + "opera.exe")) //On vérifie que le fichier "opera.exe" est bien présent
            {
                da = DefineArgs(cheminversion + "opera.exe"); //Définition des arguments
                if (da != 0)
                {
                    pc.StartInfo = p;

                    StartRH(); //Démarrage de ResourceHacker
                }
                else
                {
                    MessageBox.Show("Impossible de trouver le fichier \"opera.exe\" dans le dossier \"" + cheminversion + "\"", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }

            }
        }

        private void launcherbutton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(OperaPath + "\\launcher.exe")) //On vérifie que le fichier "opera.exe" est bien présent
            {
                da = DefineArgs(OperaPath + "\\launcher.exe"); //Définition des arguments
                if (da != 0)
                {
                    pc.StartInfo = p;

                    StartRH(); //Démarrage de ResourceHacker
                }
            }
            else
            {
                MessageBox.Show("Impossible de trouver le fichier \"launcher.exe\" dans le dossier \"" + OperaPath + "\\\"", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Checkboxes

        private void customicon_Checked(object sender, RoutedEventArgs e)
        {
            customiconpreview.Visibility = Visibility.Visible;
            iconpreview.Visibility = Visibility.Hidden;

            AppelDeOCB = false;
            originalicon.IsChecked = false;
            AppelDeOCB = true;

            var ofd = new OpenFileDialog() { Filter = "Icônes (*.ico)|*.ico" };
            if (ofd.ShowDialog() == true)
            {
                CustomIcon = ofd.FileName;
                iconpreview.Source = new BitmapImage(new Uri(new Uri(ofd.FileName).AbsoluteUri));
                customiconpreview.Visibility = Visibility.Hidden;
                iconpreview.Visibility = Visibility.Visible;
            }
            else
            {
                CancelCheck(false);
            }
        }

        private void customicon_Unchecked(object sender, RoutedEventArgs e)
        {
            if (chkoriginal)
            {
                var b = new Bitmap(typeof(MainWindow), "OperaIcons.original.png");
                iconpreview.Source = BitmapToBitmapSource(b);
            }
            else
            {
                var b = new Bitmap(typeof(MainWindow), "OperaIcons.opera.png");
                iconpreview.Source = BitmapToBitmapSource(b);
            }

            iconpreview.Visibility = Visibility.Visible;
            customiconpreview.Visibility = Visibility.Hidden;
        }

        private void originalicon_Checked(object sender, RoutedEventArgs e)
        {
            chkoriginal = true;

            var b = new Bitmap(typeof(MainWindow), "OperaIcons.original.png");
            iconpreview.Source = BitmapToBitmapSource(b);

            customicon.IsChecked = false;
        }

        private void originalicon_Unchecked(object sender, RoutedEventArgs e)
        {
            if (AppelDeOCB)
                chkoriginal = false;

            var b = new Bitmap(typeof(MainWindow), "OperaIcons.opera.png");
            iconpreview.Source = BitmapToBitmapSource(b);
        }

        #endregion

        #region RadioButtons

        private void customradiobutton_Checked(object sender, RoutedEventArgs e)
        {
            if (AppelDeCRB)
            {
                if (!delbutton.IsEnabled)
                {
                    if (!AskOperaPath())
                    {
                        CancelCheck(true);
                    }
                }
                else
                {
                    OperaPath = osgs.OperaPath;
                    RefreshVersions();
                }
            }
        }

        private void CancelCheck(bool RadioButtons)
        {
            if (RadioButtons)
            {
                if (chk32)
                {
                    _32radiobutton.IsChecked = true;
                }
                else
                {
                    _64radiobutton.IsChecked = true;
                }
            }
            else
            {
                customicon.IsChecked = false;

                if (chkoriginal)
                {
                    originalicon.IsChecked = true;
                }
            }
        }

        private void _32radiobutton_Checked(object sender, RoutedEventArgs e)
        {
            OperaPath = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%") + "\\Opera"; //Définition de OperaPath
            chk32 = true;
            sgs.WriteSettings("OperaMode", "32");
            RefreshVersions();
        }

        private void _64radiobutton_Checked(object sender, RoutedEventArgs e)
        {
            OperaPath = Environment.ExpandEnvironmentVariables("%ProgramW6432%") + "\\Opera"; //Définition de OperaPath
            chk32 = false;
            sgs.WriteSettings("OperaMode", "64");
            RefreshVersions();
        }

        #endregion

        private void refreshbutton_Click(object sender, RoutedEventArgs e) => RefreshVersions();

        private void delbutton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (delbuttonimage != null)
            {
                if ((bool)e.NewValue == true)
                {
                    delbuttonimage.Opacity = 1;
                }
                if ((bool)e.NewValue == false)
                {
                    delbuttonimage.Opacity = 0.5;
                }
            }
        }

        private void delbutton_Click(object sender, RoutedEventArgs e)
        {
            sgs.EraseSettingLine("OperaPath");
            delbutton.IsEnabled = false;

            if (_32radiobutton.IsEnabled || _64radiobutton.IsEnabled)
            {
                CancelCheck(true);
            }
            else
            {
                while (!AskOperaPath())
                {
                    if (MessageBox.Show("Voulez-vous conserver le chemin actuellement utilisé ?", "Question", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                    {
                        if (MessageBox.Show("Voulez-vous quitter l'application ?", "Quitter", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            Close();
                            break;
                        }
                    }
                }
            }
        }

        #endregion
    }
}
