using Discord;
using Discord.Modules;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class SettingsManager<GlobalSettingsT, SettingsT> : SettingsManager<SettingsT>
        where GlobalSettingsT : class, new()
        where SettingsT : class, new()
    {
        public GlobalSettingsT Global { get; private set; }

        public SettingsManager(string name)
            : base(name)
        {
        }

        public override void LoadConfigs()
        {
            var path = $"{_dir}/global.json";
            if (File.Exists(path))
                Global = JsonConvert.DeserializeObject<GlobalSettingsT>(File.ReadAllText(path));
            else
                Global = new GlobalSettingsT();

            base.LoadConfigs();
        }
        
        public async Task SaveGlobal(GlobalSettingsT settings)
        {
            while (true)
            {
                try
                {
                    using (var fs = new FileStream($"{_dir}/global.json", FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(fs))
                        await writer.WriteAsync(JsonConvert.SerializeObject(Global));
                    break;
                }
                catch (IOException) //In use
                {
                    await Task.Delay(1000);
                }
            }
        }
    }

    public class SettingsManager<SettingsT>
		where SettingsT : class, new()
	{
		public string Directory => _dir;
		protected readonly string _dir;

		public IEnumerable<KeyValuePair<ulong, SettingsT>> AllServers => _servers;
		private ConcurrentDictionary<ulong, SettingsT> _servers;

		public SettingsManager(string name)
		{
			_dir = $"./config/{name}";
			System.IO.Directory.CreateDirectory(_dir);

			LoadConfigs();
		}
        
		public bool RemoveServer(ulong id)
		{
			SettingsT settings;
            if (_servers.TryRemove(id, out settings))
            {
                var path = $"{_dir}/{id}.json";
                if (File.Exists(path))
                    File.Delete(path);
                return true;
            }
            return false;
		}

		public virtual void LoadConfigs()
        {
            var servers = System.IO.Directory.GetFiles(_dir)
                .Select(x =>
                {
                    ulong id;
                    if (ulong.TryParse(Path.GetFileNameWithoutExtension(x), out id))
                        return id;
                    else
                        return (ulong?)null;
                })
                .Where(x => x.HasValue)
                .ToDictionary(x => x.Value, x =>
                {
                    string path = $"{_dir}/{x}.json";
                    if (File.Exists(path))
                        return JsonConvert.DeserializeObject<SettingsT>(File.ReadAllText(path));
                    else
                        return new SettingsT();
                });

            _servers = new ConcurrentDictionary<ulong, SettingsT>(servers);
		}

		public SettingsT Load(Server server)
			=> Load(server.Id);
		public SettingsT Load(ulong serverId)
		{
			SettingsT result;
			if (_servers.TryGetValue(serverId, out result))
				return result;
			else
				return new SettingsT();
		}

		public Task Save(Server server, SettingsT settings)
			=> Save(server.Id, settings);
		public Task Save(KeyValuePair<ulong, SettingsT> pair)
			=> Save(pair.Key, pair.Value);
        public async Task Save(ulong serverId, SettingsT settings)
		{
			_servers[serverId] = settings;

			while (true)
			{
				try
				{
					using (var fs = new FileStream($"{_dir}/{serverId}.json", FileMode.Create, FileAccess.Write, FileShare.None))
					using (var writer = new StreamWriter(fs))
						await writer.WriteAsync(JsonConvert.SerializeObject(settings));
					break;
				}
				catch (IOException) //In use
				{
					await Task.Delay(1000);
				}
			}
		}
	}

	public class SettingsService : IService
	{
		public void Install(DiscordClient client) { }

		public SettingsManager<SettingsT> AddModule<ModuleT, SettingsT>(ModuleManager manager)
			where SettingsT : class, new()
            => new SettingsManager<SettingsT>(manager.Id);

        public SettingsManager<GlobalSettingsT, SettingsT> AddModule<ModuleT, GlobalSettingsT, SettingsT>(ModuleManager manager)
            where GlobalSettingsT : class, new()
            where SettingsT : class, new()
            => new SettingsManager<GlobalSettingsT, SettingsT>(manager.Id);
    }
}
