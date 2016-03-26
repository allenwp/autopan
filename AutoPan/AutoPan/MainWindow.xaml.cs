using Discord;
using Discord.Audio;
using Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Serialization;

namespace AutoPan
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly string CONNECTION_SETTINGS_PATH = ".\\ConnectionSettings.xml";

        enum UIState { LoggedOut, Connecting, LoggedIn, ConnectedToChannel }

        DiscordClient client = null;
        Channel channel = null;

        List<Channel> channels = new List<Channel>();

        ConnectionSettings lastSuccessfulConnectionSettings;

        UIState state = UIState.LoggedOut;
        UIState State
        {
            get
            {
                return state;
            }
            set
            {
                state = value;
                switch (state)
                {
                    case UIState.LoggedIn:
                        emailTextBox.IsEnabled = false;
                        passwordBox.IsEnabled = false;
                        loginButton.IsEnabled = false;
                        savePasswordCheckBox.IsEnabled = false;

                        logoutButton.IsEnabled = true;

                        channelComboBox.IsEnabled = true;
                        connectButton.IsEnabled = true;

                        disconnectButton.IsEnabled = false;
                        break;
                    case UIState.ConnectedToChannel:
                        emailTextBox.IsEnabled = false;
                        passwordBox.IsEnabled = false;
                        loginButton.IsEnabled = false;
                        savePasswordCheckBox.IsEnabled = false;

                        logoutButton.IsEnabled = true;

                        channelComboBox.IsEnabled = false;
                        connectButton.IsEnabled = false;

                        disconnectButton.IsEnabled = true;
                        break;
                    case UIState.Connecting:
                        emailTextBox.IsEnabled = false;
                        passwordBox.IsEnabled = false;
                        loginButton.IsEnabled = false;
                        savePasswordCheckBox.IsEnabled = false;

                        logoutButton.IsEnabled = false;

                        channelComboBox.IsEnabled = false;
                        connectButton.IsEnabled = false;

                        disconnectButton.IsEnabled = false;
                        break;
                    case UIState.LoggedOut:
                    default:
                        emailTextBox.IsEnabled = true;
                        passwordBox.IsEnabled = true;
                        loginButton.IsEnabled = true;
                        savePasswordCheckBox.IsEnabled = true;

                        logoutButton.IsEnabled = false;

                        channelComboBox.IsEnabled = false;
                        connectButton.IsEnabled = false;

                        disconnectButton.IsEnabled = false;
                        break;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            Closing += MainWindow_Closing;

            OnLogMessage(this, new LogMessageEventArgs(LogSeverity.Info, "Auto Pan", "Thanks for using Auto Pan! Here's how to set it up: \n\n- Auto Pan requires you to create a second Discord user account. I recommend adding \"[Auto Pan]\" it's username.\n\n-Your second account needs access to the server and voice channel you are using with your primary account.\n\n-Use this second account to log in above.\n\n- Auto Pan will output panned audio from the voice channel it's connected to.\n\n- Use Discord normally with your primary account for transmitting your voice.\n\n- Mute each individual user in Discord to prevent duplicate audio/echo.\n\nThis is version 1 – Keep an eye out for new updates at: allenwp.github.io/autopan", null));
            logScrollViewer.ScrollToTop();
            
            try
            {
                using (StreamReader reader = new StreamReader(CONNECTION_SETTINGS_PATH, Encoding.UTF8))
                {
                    XmlSerializer serializer = new XmlSerializer(lastSuccessfulConnectionSettings.GetType());
                    lastSuccessfulConnectionSettings = (ConnectionSettings)serializer.Deserialize(reader);

                    if(!string.IsNullOrWhiteSpace(lastSuccessfulConnectionSettings.Email))
                    {
                        emailTextBox.Text = lastSuccessfulConnectionSettings.Email;
                        if(!string.IsNullOrWhiteSpace(lastSuccessfulConnectionSettings.Password))
                        {
                            passwordBox.Password = lastSuccessfulConnectionSettings.Password;
                            savePasswordCheckBox.IsChecked = true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Just reset to defaults (no settings)
                lastSuccessfulConnectionSettings = new ConnectionSettings();
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Remove password if they unchecked the save password option after the last login
            if(savePasswordCheckBox.IsChecked != true)
            {
                lastSuccessfulConnectionSettings.Password = string.Empty;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(CONNECTION_SETTINGS_PATH, false, Encoding.UTF8))
                {
                    XmlSerializer serializer = new XmlSerializer(lastSuccessfulConnectionSettings.GetType());
                    serializer.Serialize(writer, lastSuccessfulConnectionSettings);
                }
            }
            catch (Exception)
            {
                // meh, we tried. No big deal if this failed -- might as well just continue to quit.
            }
        }

        private async void OnLogin(object sender, RoutedEventArgs e)
        {
            State = UIState.Connecting;
            client = new DiscordClient(x =>
            {
                x.AppName = "Auto Pan";
                x.AppUrl = "http://allenwp.github.io/autopan";
                x.MessageCacheSize = 0;
                x.UsePermissionsCache = false;
                x.EnablePreUpdateEvents = true;
                x.LogLevel = LogSeverity.Info;
                x.LogHandler = OnLogMessage;
            })
            .UsingAudio(x =>
            {
                x.Mode = AudioMode.Incoming;
                x.EnableMultiserver = false;
                x.EnableEncryption = true;
            })
            .AddService<HttpService>();

            try
            {
                await client.Connect(emailTextBox.Text, passwordBox.Password);
                
                bool alreadyInVoice = false;
                foreach(Server server in client.Servers)
                {
                    User u = server.GetUser(client.CurrentUser.Id);
                    if (u != null && u.VoiceChannel != null)
                    {
                        alreadyInVoice = true;
                    }
                }

                if(alreadyInVoice)
                {
                    // Discord does not allow a user to be logged into the same voice channel from multiple locations at the same time. (you get booted out of the voice channel in discord client if you connect through a different client)
                    client.Log.Error("Login", $"Login failed because this user is already connected to a voice channel. Please create your own separate \"bot\" account for use with Auto Pan.", null);
                    State = UIState.LoggedOut;
                }
                else
                {
                    client.SetGame("Auto Pan"); // Since we know this is a "bot" account, it can be playing a different game than the main user account.

                    lastSuccessfulConnectionSettings.Email = emailTextBox.Text;
                    lastSuccessfulConnectionSettings.Password = (savePasswordCheckBox.IsChecked == true) ? passwordBox.Password : string.Empty;

                    channels.Clear();
                    List<string> comboItems = new List<string>();

                    int selectionIndex = 0;
                    int comboIndex = 0;
                    foreach (Server server in client.Servers)
                    {
                        foreach (Channel c in server.VoiceChannels)
                        {
                            channels.Add(c);
                            string channelString = string.Format("[{0}] {1}", server.Name, c.Name);
                            comboItems.Add(channelString);
                            if (channelString == lastSuccessfulConnectionSettings.LastVoiceChannel)
                            {
                                selectionIndex = comboIndex;
                            }
                            //foreach(User u in c.Users)
                            //{
                            //    OnLogMessage(this, new LogMessageEventArgs(LogSeverity.Info, "", string.Format("{0} {1} {2}", server.Name, c.Name, u.VoiceChannel), null));
                            //}
                            comboIndex++;
                        }
                    }

                    if (channels.Count != 0)
                    {
                        channelComboBox.ItemsSource = comboItems;
                        channelComboBox.SelectedIndex = selectionIndex;
                        State = UIState.LoggedIn;
                    }
                    else
                    {
                        client.Log.Error("Login", $"Login failed because this user has no voice channels to connect to!", null);
                        State = UIState.LoggedOut;
                    }
                }

            }
            catch (Exception ex)
            {
                client.Log.Error("Login", $"Login Failed", ex);
                State = UIState.LoggedOut;
            }
        }

        private async void OnConnect(object sender, RoutedEventArgs e)
        {
            State = UIState.Connecting;
            try
            {
                this.channel = channels[channelComboBox.SelectedIndex];
                IAudioClient audioClient = await channel.JoinAudio();
                State = UIState.ConnectedToChannel;
                lastSuccessfulConnectionSettings.LastVoiceChannel = channelComboBox.SelectedItem.ToString();
            }
            catch (Exception ex)
            {
                client.Log.Error("Voice Channel", $"Failed to connect to voice channel", ex);
                State = UIState.LoggedIn; // Maaaybe?? Who knows what the state is here...
            }
        }

        private void OnLogMessage(object sender, LogMessageEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                string oldText = logTextBlock.Text;

                int maxExistingLength = 1000;
                if(oldText.Length > maxExistingLength)
                {
                    oldText = oldText.Substring(oldText.Length - maxExistingLength, maxExistingLength);
                }

                ////Color
                //ConsoleColor color;
                //switch (e.Severity)
                //{
                //    case LogSeverity.Error: color = ConsoleColor.Red; break;
                //    case LogSeverity.Warning: color = ConsoleColor.Yellow; break;
                //    case LogSeverity.Info: color = ConsoleColor.White; break;
                //    case LogSeverity.Verbose: color = ConsoleColor.Gray; break;
                //    case LogSeverity.Debug: default: color = ConsoleColor.DarkGray; break;
                //}

                //Exception
                string exMessage;
                Exception ex = e.Exception;
                if (ex != null)
                {
                    while (ex is AggregateException && ex.InnerException != null)
                        ex = ex.InnerException;
                    exMessage = ex.Message;
                }
                else
                    exMessage = null;

                //Source
                string sourceName = e.Source?.ToString();

                //Text
                string text;
                if (e.Message == null)
                {
                    text = exMessage ?? "";
                    exMessage = null;
                }
                else
                    text = e.Message;

                //Build message
                StringBuilder builder = new StringBuilder(oldText.Length + 1 + text.Length + (sourceName?.Length ?? 0) + (exMessage?.Length ?? 0) + 5);
                builder.Append(oldText);
                if (!string.IsNullOrEmpty(oldText))
                {
                    builder.Append("\n\n");
                }
                if (sourceName != null)
                {
                    builder.Append('[');
                    builder.Append(sourceName);
                    builder.Append("] ");
                }
                for (int i = 0; i < text.Length; i++)
                {
                    //Strip control chars
                    char c = text[i];
                    if (!char.IsControl(c) || c == '\n') // allow newlines
                        builder.Append(c);
                }
                if (exMessage != null)
                {
                    builder.Append(": ");
                    builder.Append(exMessage);
                }

                text = builder.ToString();
            
                // TODO: colour and filter based on debug/release
                logTextBlock.Text = text;

                logScrollViewer.ScrollToBottom();
            }));
        }

        private async void OnDisconnect(object sender, RoutedEventArgs e)
        {
            State = UIState.Connecting;
            try
            {
                await channel.LeaveAudio();
                State = UIState.LoggedIn;
            }
            catch (Exception ex)
            {
                client.Log.Error("Voice Channel", $"Failed to disconnect from voice channel", ex);
                State = UIState.ConnectedToChannel; // Maaaybe?? Who knows what the state is here...
            }
        }

        private async void OnLogout(object sender, RoutedEventArgs e)
        {
            State = UIState.Connecting;
            try
            {
                await client.Disconnect();
            }
            catch (Exception ex)
            {
                client.Log.Error("Log Out", $"Got an error while trying to log out.", ex);
            }
            State = UIState.LoggedOut;
        }
    }
}
