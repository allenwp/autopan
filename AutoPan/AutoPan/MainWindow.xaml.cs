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
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Connect(object sender, RoutedEventArgs e)
        {
            while (true)
            {
                DiscordClient _client = new DiscordClient(x =>
            {
                x.AppName = "Auto Pan";
                x.AppUrl = "http://allenwp.github.io/autopan";
                x.MessageCacheSize = 0;
                x.UsePermissionsCache = false;
                x.EnablePreUpdateEvents = true;
                x.LogLevel = LogSeverity.Info;
                //x.LogHandler = OnLogMessage;
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
                    await _client.Connect("email", "password");
                    _client.SetGame("Discord.Net");

                    bool hasConnected = false;

                    foreach (Server server in _client.Servers)
                    {
                        foreach (Channel channel in server.AllChannels)
                        {
                            await channel.SendMessage("'sup?");
                        }

                        if (!hasConnected)
                        {
                            foreach (Channel channel in server.VoiceChannels)
                            {
                                IAudioClient audioClient = await channel.JoinAudio();
                                hasConnected = true;
                            }
                        }
                    }

                    break;
                }
                catch (Exception ex)
                {
                    _client.Log.Error($"Login Failed", ex);
                    await Task.Delay(_client.Config.FailedReconnectDelay);
                }
            }
        }
    }
}
