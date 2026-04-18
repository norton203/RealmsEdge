using RealmsEdge.Shared.Interfaces;

namespace RealmsEdge
{
    public partial class App : Application
    {
        private readonly IDatabaseService _database;

        public App(IDatabaseService database)
        {
            _database = database;
            InitializeComponent();
        }

        protected override Window CreateWindow(
            IActivationState? activationState)
        {
            // Fire async init without blocking
            _ = _database.InitialiseAsync();
            return new Window(new MainPage());
        }
    }
}