using Discord;
using Discord.Audio;
using Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Serialization;

namespace AutoPan
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly string CONNECTION_SETTINGS_FILE = "ConnectionSettings.xml";
        readonly string USER_SETTINGS_FILE = "UserSettings.xml";
        public string SavePath {
            get
            {
                string result = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Auto Pan");
                Directory.CreateDirectory(result);
                return result;
            }
        }

        enum UIState { LoggedOut, Connecting, LoggedIn, ConnectedToChannel }

        DiscordClient client = null;
        IAudioClient audioClient = null;
        Channel channel = null;

        List<Channel> channels = new List<Channel>();

        ConnectionSettings lastSuccessfulConnectionSettings;

        /// <summary>
        /// Serialized and saved copy of all previous user settings. This includes settings for users that are not in the current channel.
        /// </summary>
        Dictionary<ulong, UserSettings> savedUserSettings;

        /// <summary>
        /// Data that matches the users in the current voice channel (and therefore the view as well).
        /// </summary>
        ObservableCollection<UserSettings> connectedUserSettings = new ObservableCollection<UserSettings>();

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

            DataContext = connectedUserSettings;

            Closing += MainWindow_Closing;

            OnLogMessage(this, new LogMessageEventArgs(LogSeverity.Info, null, "Thanks for using Auto Pan!\n\nSetup Instructions:\n\n- Auto Pan requires you to create a second Discord user account. I recommend adding \"[Auto Pan]\" it's username.\n\n-Your second account needs access to the server and voice channel you are using with your primary account.\n\n-Use this second account to log in above.\n\n- Auto Pan will output panned audio from the voice channel it's connected to.\n\n- Mute yourself in Auto Pan so you don't hear yourself speaking.\n\n- Use Discord normally with your primary account for transmitting your voice.\n\n- Mute each individual user in Discord to prevent duplicate audio/echo.\n\n---\n\nVersion 0.1\n\nKeep an eye out for new updates at: allenwp.github.io/autopan\n\n---", null));
            logScrollViewer.ScrollToTop();

            LoadConnectionSettings();
            LoadUserSettings();
        }

        private void LoadConnectionSettings()
        {
            try
            {
                using (StreamReader reader = new StreamReader(Path.Combine(SavePath, CONNECTION_SETTINGS_FILE), Encoding.UTF8))
                {
                    XmlSerializer serializer = new XmlSerializer(lastSuccessfulConnectionSettings.GetType());
                    lastSuccessfulConnectionSettings = (ConnectionSettings)serializer.Deserialize(reader);

                    if (!string.IsNullOrWhiteSpace(lastSuccessfulConnectionSettings.Email))
                    {
                        emailTextBox.Text = lastSuccessfulConnectionSettings.Email;
                        if (!string.IsNullOrWhiteSpace(lastSuccessfulConnectionSettings.Password))
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

        private void LoadUserSettings()
        {
            try
            {
                using (StreamReader reader = new StreamReader(Path.Combine(SavePath, USER_SETTINGS_FILE), Encoding.UTF8))
                {
                    savedUserSettings = new Dictionary<ulong, UserSettings>();
                    XmlSerializer serializer = new XmlSerializer(typeof(UserSettings[]));
                    UserSettings[] userSettingsArray = (UserSettings[])serializer.Deserialize(reader);
                    foreach(UserSettings settings in userSettingsArray)
                    {
                        savedUserSettings[settings.Id] = settings;
                        settings.PropertyChanged += Settings_PropertyChanged; // We need to be subscribed to all of these to know when auto panning needs to happen.
                    }
                }
            }
            catch (Exception)
            {
                // Just reset to defaults (no settings)
                savedUserSettings = new Dictionary<ulong, UserSettings>();
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Remove password if they unchecked the save password option after the last login
            if(savePasswordCheckBox.IsChecked != true)
            {
                lastSuccessfulConnectionSettings.Password = string.Empty;
            }

            // If these fail, it would probably cause more problems if I try to abort quitting the program... Might as well just continue to quit.
            SaveUserSettings();
            SaveConnectionSettings();
        }

        /// <returns>true if succeeded, false otherwise</returns>
        private bool SaveUserSettings()
        {
            bool result = false;
            try
            {
                using (StreamWriter writer = new StreamWriter(Path.Combine(SavePath, USER_SETTINGS_FILE), false, Encoding.UTF8))
                {
                    UserSettings[] values = savedUserSettings.Values.ToArray();
                    XmlSerializer serializer = new XmlSerializer(values.GetType());
                    serializer.Serialize(writer, values);
                }
                result = true;
            }
            catch (Exception)
            {
            }

            return result;
        }

        /// <returns>true if succeeded, false otherwise</returns>
        private bool SaveConnectionSettings()
        {
            bool result = false;
            try
            {
                using (StreamWriter writer = new StreamWriter(Path.Combine(SavePath, CONNECTION_SETTINGS_FILE), false, Encoding.UTF8))
                {
                    XmlSerializer serializer = new XmlSerializer(lastSuccessfulConnectionSettings.GetType());
                    serializer.Serialize(writer, lastSuccessfulConnectionSettings);
                }
                result = true;
            }
            catch (Exception)
            {
            }

            return result;
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

            client.UserUpdated += Client_UserUpdated;
            client.MessageReceived += Client_MessageReceived;

            // The only way I know how to get the audioService right now to subscribe to the userIsSpeakingUpdated thingy
            AudioService audioService = null;
            foreach (var service in client.Services)
            {
                audioService = service as AudioService;
                if(audioService != null)
                {
                    audioService.UserIsSpeakingUpdated += AudioService_UserIsSpeakingUpdated;
                    break;
                }
            }

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
                // Disconnect in case it actually did manage to connect previoulsy.
                try
                {
                    await client.Disconnect();
                }
                catch (Exception)
                {
                    // We tried. Must not have connected in the first place.
                }
                client.Log.Error("Login", $"Login Failed", ex);
                State = UIState.LoggedOut;
            }
        }

        private void AudioService_UserIsSpeakingUpdated(object sender, UserIsSpeakingEventArgs e)
        {
            if(savedUserSettings.ContainsKey(e.User.Id))
            {
                savedUserSettings[e.User.Id].UserIsSpeaking = e.IsSpeaking;
            }
        }

        private void Client_UserUpdated(object sender, UserUpdatedEventArgs e)
        {
            // Update name if it's changed:
            // TODO: Test this more... Can't change my username too fast with Discord...
            if (e.Before.Name != e.After.Name
                && savedUserSettings.ContainsKey(e.After.Id))
            {
                savedUserSettings[e.After.Id].Name = e.After.Name;
            }
            
            if (e.Before.VoiceChannel == channel && e.After.VoiceChannel != channel)
            {
                // User has left voice
                Dispatcher.Invoke((Action)(() =>
                {
                    UserSettings settingsToRemove = null;
                    foreach (var userSettings in connectedUserSettings)
                    {
                        if (e.After.Id == userSettings.Id)
                        {
                            settingsToRemove = userSettings;
                            break;
                        }
                    }
                    RemoveUser(settingsToRemove);
                }));
            }
            else if (e.Before.VoiceChannel != channel && e.After.VoiceChannel == channel)
            {
                // User joined this voice channel or has changed voice channel to join this channel
                AddUser(e.After);
            }
        }

        private void Client_MessageReceived(object sender, MessageEventArgs e)
        {
            if(e.Message.IsMentioningMe())
            {
                // We were mentioned! Let them know about Auto Pan:
                e.Message.Channel.SendMessage("Hi there! I'm an Auto Pan bot. Auto Pan is a tool that performs a stereo spread to make it easier to distinguish voices. Find out more at http://allenwp.github.io/autopan");
            }
        }

        private async void OnConnect(object sender, RoutedEventArgs e)
        {
            State = UIState.Connecting;
            try
            {
                this.channel = channels[channelComboBox.SelectedIndex];
                audioClient = await channel.JoinAudio();
                
                State = UIState.ConnectedToChannel;
                lastSuccessfulConnectionSettings.LastVoiceChannel = channelComboBox.SelectedItem.ToString();

                foreach(var user in channel.Users)
                {
                    AddUser(user);
                }
            }
            catch (Exception ex)
            {
                // in case it's actually already connected:
                if (channel != null)
                {
                    try
                    {
                        await channel.LeaveAudio();
                    }
                    catch (Exception)
                    {
                        // we tried. Maybe they just never connected in the first place.
                    }
                }
                client.Log.Error("Voice Channel", $"Failed to connect to voice channel", ex);
                State = UIState.LoggedIn; // Maaaybe?? Who knows what the state is here...
            }
        }

        private void AddUser(User user)
        {
            // FIXME: Sometimes currentUser can be null here... figure out where this comes from (it's either from connect or user updated)
            // can be repliacted by stopping debugingg (which doesn't correctly log out), then re-opening and waiting for things to happen.
            if (user.Id != client.CurrentUser.Id)
            {
                Dispatcher.Invoke((Action)(() =>
                {
                    UserSettings settingsToAdd;
                    if (savedUserSettings.ContainsKey(user.Id))
                    {
                        UserSettings settings = savedUserSettings[user.Id];

                        // Update the saved name, since that might have changed while they were offline:
                        settings.Name = user.Name;

                        settingsToAdd = savedUserSettings[user.Id];

                        // listen for when to trigger an auto pan
                        settings.PropertyChanged += Settings_PropertyChanged;
                    }
                    else
                    {
                        UserSettings newSettings = new UserSettings(user.Id) { Name = user.Name };
                        savedUserSettings[user.Id] = newSettings;
                        settingsToAdd = newSettings;
                    }

                    // Keep the connectedUserSettings sorted by ID so that users are always auto panned to the same position relative to other users.
                    int index = 0;
                    for(; index < connectedUserSettings.Count && connectedUserSettings[index].Id < settingsToAdd.Id; index++)
                    { }
                    connectedUserSettings.Insert(index, settingsToAdd);

                    AutoPan();
                }));
            }
        }

        private void RemoveUser(UserSettings user)
        {
            connectedUserSettings.Remove(user);
            AutoPan();
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
                if (!string.IsNullOrWhiteSpace(sourceName))
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

                CleanupVoiceChannel();
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

            CleanupVoiceChannel();
        }

        private void CleanupVoiceChannel()
        {
            connectedUserSettings.Clear();
            audioClient = null;
        }

        private void OnResetVolumeSlider(object sender, MouseButtonEventArgs e)
        {
            Slider slider = sender as Slider;
            if(slider != null)
            {
                slider.Value = 100;
            }
        }

        private void OnResetPanSlider(object sender, MouseButtonEventArgs e)
        {
            Slider slider = sender as Slider;
            if (slider != null)
            {
                slider.Value = 0f;
            }
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if("Audible" == e.PropertyName || "AutoPan" == e.PropertyName)
            {
                AutoPan();
            }
        }

        private void AutoPan()
        {
            int numberOfAutoPans = 0;
            foreach(var userSetting in connectedUserSettings)
            {
                if(userSetting.Audible && userSetting.AutoPan)
                {
                    numberOfAutoPans++;
                }
            }

            if (numberOfAutoPans > 0)
            {
                int autoPanCounter = 0;
                foreach (var userSetting in connectedUserSettings)
                {
                    if (userSetting.Audible && userSetting.AutoPan)
                    {
                        float pan = 0;
                        if (numberOfAutoPans > 1)
                        {
                            pan = (float)autoPanCounter / (float)(numberOfAutoPans - 1); // figure out pan between 0 and 1
                            pan = (pan * 2f) - 1f; // scale to -1 to 1 range
                        }
                        userSetting.Pan = pan;

                        autoPanCounter++;
                    }
                }
            }
        }
    }
}
