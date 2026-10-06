
namespace CareComply.Domain.Navigation
{
    public sealed class NavigationModule
    {
        private NavigationModule()
        {
        }

        public int Id { get; private set; }
        public string Key { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public int SortOrder { get; private set; }
        public bool IsActive { get; private set; }
    }
}
