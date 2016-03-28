using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AutoPan
{
    public class UserSettings : INotifyPropertyChanged
    {
        /// <summary>
        /// For serialization
        /// </summary>
        private UserSettings()
        {
        }

        public UserSettings(ulong id)
        {
            this.id = id;
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        #endregion

        private ulong id;
        /// <summary>
        /// Unique Discord ID for user.
        /// Setter should be private, but is public for serialization.
        /// </summary>
        public ulong Id
        {
            get
            {
                return id;
            }
            set // Only intended for serialization
            {
                id = value;
            }
        }

        private bool audible = true;
        /// <summary>
        /// "Muted" when set to false.
        /// </summary>
        public bool Audible
        {
            get
            {
                return audible;
            }
            set
            {
                audible = value;

                NotifyPropertyChanged("Audible");
                NotifyPropertyChanged("ManualPanAvailable");
            }
        }

        private int volume = 100;
        /// <summary>
        /// Range: 0 to 200. 100 is "100%" (unmodified).
        /// </summary>
        public int Volume
        {
            get
            {
                return volume;
            }

            set
            {
                volume = value;

                NotifyPropertyChanged("Volume");
            }
        }

        private bool autoPan = true;
        public bool AutoPan
        {
            get
            {
                return autoPan;
            }

            set
            {
                if(!autoPan)
                {
                    lastManualPan = Pan;
                }
                autoPan = value;
                if(!autoPan)
                {
                    Pan = lastManualPan;
                }

                NotifyPropertyChanged("AutoPan");
                NotifyPropertyChanged("ManualPanAvailable");
            }
        }

        public bool ManualPanAvailable
        {
            get
            {
                return audible && !autoPan;
            }
        }

        private float pan = 0;
        /// <summary>
        /// Range: -1 to 1. Center is 0, Left is -1, Right is 1.
        /// </summary>
        [XmlIgnore]
        public float Pan
        {
            get
            {
                return pan;
            }

            set
            {
                pan = value;
                if(!AutoPan)
                {
                    lastManualPan = value;
                }

                NotifyPropertyChanged("Pan");
            }
        }

        /// <summary>
        /// Used for remembering what the last manual pan was when switching between manual and auto-pan.
        /// Range: -1 to 1. Center is 0, Left is -1, Right is 1.
        /// Setter should be private, but is public for serialization.
        /// </summary>
        private float lastManualPan = 0;
        public float LastManualPan
        {
            get
            {
                return lastManualPan;
            }
            set // Only intended for serialization
            {
                lastManualPan = value;
            }
        }
        
        private string name = string.Empty;
        /// <summary>
        /// User name may change at anytime.
        /// </summary>
        [XmlIgnore]
        public string Name
        {
            get
            {
                return name;
            }

            set
            {
                name = value;

                NotifyPropertyChanged("Name");
            }
        }

        private bool userIsSpeaking = false;
        [XmlIgnore]
        public bool UserIsSpeaking
        {
            get
            {
                return userIsSpeaking;
            }

            set
            {
                userIsSpeaking = value;

                NotifyPropertyChanged("UserIsSpeaking");
            }
        }
    }
}
