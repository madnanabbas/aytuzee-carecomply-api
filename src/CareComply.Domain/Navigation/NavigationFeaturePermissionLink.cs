
namespace CareComply.Domain.Navigation
{
    public sealed class NavigationFeaturePermissionLink
    {
        private NavigationFeaturePermissionLink()
        {

        }

        public int FeatureId { get; private set; }
        public int PermissionId { get; private set; }
    }
}
