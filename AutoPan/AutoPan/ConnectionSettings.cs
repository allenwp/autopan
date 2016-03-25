using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPan
{
    [Serializable]
    public struct ConnectionSettings
    {
        public string Email;
        public string Password;
        public string LastVoiceChannel;
    }
}
