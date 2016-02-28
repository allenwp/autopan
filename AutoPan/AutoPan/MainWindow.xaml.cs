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
        DiscordClient client = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void OnLogin(object sender, RoutedEventArgs e)
        {
            while (true)
            {
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
                    client.SetGame("Auto Pan");
                    break;
                }
                catch (Exception ex)
                {
                    client.Log.Error($"Login Failed", ex);
                    await Task.Delay(client.Config.FailedReconnectDelay);
                }
            }
        }

        private async void OnConnect(object sender, RoutedEventArgs e)
        {
            bool hasConnected = false;

            try
            {
                foreach (Server server in client.Servers)
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
            }
            catch (Exception ex)
            {
                //ruh roh.
            }
        }

        private void OnLogMessage(object sender, LogMessageEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                string oldText = logTextBlock.Text;

                //Color
                ConsoleColor color;
                switch (e.Severity)
                {
                    case LogSeverity.Error: color = ConsoleColor.Red; break;
                    case LogSeverity.Warning: color = ConsoleColor.Yellow; break;
                    case LogSeverity.Info: color = ConsoleColor.White; break;
                    case LogSeverity.Verbose: color = ConsoleColor.Gray; break;
                    case LogSeverity.Debug: default: color = ConsoleColor.DarkGray; break;
                }

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
    }
}
