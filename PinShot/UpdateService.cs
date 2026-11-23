using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace PinShot
{
    internal sealed class UpdateService
    {
        private const string RepositoryUrl = "https://github.com/adrian-system01/PinShot";
        private readonly UpdateManager _manager = new UpdateManager(
            new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));

        public bool IsInstalled => _manager.IsInstalled;

        public async Task<VelopackAsset?> CheckAndDownloadAsync()
        {
            var update = await _manager.CheckForUpdatesAsync();
            if (update == null) return null;

            await _manager.DownloadUpdatesAsync(update);
            return update.TargetFullRelease;
        }

        public void ApplyAndRestart(VelopackAsset update)
        {
            _manager.ApplyUpdatesAndRestart(update);
        }
    }
}
