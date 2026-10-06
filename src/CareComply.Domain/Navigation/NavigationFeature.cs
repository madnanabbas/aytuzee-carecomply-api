
namespace CareComply.Domain.Navigation
{
    public sealed class NavigationFeature
    {
        private NavigationFeature()
        {

        }

        public int Id { get; private set; }
        public int ModuleId { get; private set; }
        public int? ParentFeatureId { get; private set; }
        public string Key { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public string Route { get; private set; } = string.Empty;
        public string IconKey { get; private set; } = string.Empty;
        public int SortOrder { get; private set; }
        public bool IsActive { get; private set; }
    }
}
