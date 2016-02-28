using Discord;
using Discord.Audio;
using Services;
using System;
using System.Collections.Generic;
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

namespace AutoPan
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        enum UIState { LoggedOut, Connecting, LoggedIn, ConnectedToChannel }

        DiscordClient client = null;
        Channel channel = null;

        List<Channel> channels = new List<Channel>();

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
                client.SetGame("Auto Pan"); // TODO: Only do this if a bot is needed for Auto Pan

                channels.Clear();
                List<string> comboItems = new List<string>();

                foreach (Server server in client.Servers)
                {
                    foreach (Channel c in server.VoiceChannels)
                    {
                        channels.Add(c);
                        comboItems.Add(string.Format("[{0}] {1}", server.Name, c.Name));
                    }
                }

                if(channels.Count != 0)
                {
                    channelComboBox.ItemsSource = comboItems;
                    channelComboBox.SelectedIndex = 0;
                    State = UIState.LoggedIn;
                }
                else
                {
                    client.Log.Error($"Login failed because this user has no voice channels to connect to!", null);
                    State = UIState.LoggedOut;
                }

            }
            catch (Exception ex)
            {
                client.Log.Error($"Login Failed", ex);
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
            }
            catch (Exception ex)
            {
                client.Log.Error($"Failed to connect to voice channel", ex);
                State = UIState.LoggedIn; // Maaaybe?? Who knows what the state is here...
            }
        }

        private void OnLogMessage(object sender, LogMessageEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                string oldText = logTextBlock.Text;

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
                    builder.Append('\n');
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
                    if (!char.IsControl(c))
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
                client.Log.Error($"Failed to disconnect from voice channel", ex);
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
                client.Log.Error($"Got an error while trying to log out.", ex);
            }
            State = UIState.LoggedOut;
        }
    }
}
