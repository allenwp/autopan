using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPan
{
    public class UserSettings
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

        private ulong id;
        /// <summary>
        /// Unique Discord ID for user. Setter is just for serialization. You shouldn't actually change this.
        /// </summary>
        public ulong Id
        {
            get
            {
                return id;
            }
            set
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
            }
        }
        
        [NonSerialized]
        private float pan = 0;
        /// <summary>
        /// Range: -1 to 1. Center is 0, Left is -1, Right is 1.
        /// </summary>
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
            }
        }

        /// <summary>
        /// Used for remembering what the last manual pan was when switching between manual and auto-pan.
        /// Range: -1 to 1. Center is 0, Left is -1, Right is 1.
        /// </summary>
        float lastManualPan = 0;

        [NonSerialized]
        private string name = string.Empty;
        /// <summary>
        /// User name may change at anytime.
        /// </summary>
        public string Name
        {
            get
            {
                return name;
            }

            set
            {
                name = value;
            }
        }
    }
}
