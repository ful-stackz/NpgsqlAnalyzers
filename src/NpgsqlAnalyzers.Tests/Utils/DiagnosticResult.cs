using Microsoft.CodeAnalysis;

namespace NpgsqlAnalyzers.Tests.Utils
{
    internal struct DiagnosticResult
    {
        private DiagnosticResultLocation[] _locations;

        public DiagnosticResultLocation[] Locations
        {
            get
            {
                if (_locations is null)
                {
                    _locations = new DiagnosticResultLocation[] { };
                }

                return _locations;
            }
            set
            {
                _locations = value;
            }
        }

        public DiagnosticSeverity Severity { get; set; }

        public string Id { get; set; }

        public string Message { get; set; }

        public string Path =>
            Locations.Length > 0 ? Locations[0].Path : string.Empty;

        public int Line =>
            Locations.Length > 0 ? Locations[0].Line : -1;

        public int Column =>
            Locations.Length > 0 ? Locations[0].Column : -1;
    }
}
