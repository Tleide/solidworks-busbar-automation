using System;
using System.Collections.Generic;
using System.Linq;

namespace BusbarAutomation.Core.Domain
{
    internal enum AssemblyDeviceKind
    {
        Fuse,
        Breaker
    }

    internal sealed class AssemblyPortSnapshot
    {
        public string Name { get; private set; }
        public Point3 Position { get; private set; }

        public AssemblyPortSnapshot(string name, Point3 position)
        {
            Name = name;
            Position = position;
        }
    }

    internal sealed class AssemblyDeviceSnapshot
    {
        private readonly Dictionary<string, AssemblyPortSnapshot> _ports;

        public string Id { get; private set; }
        public string SourceComponentName { get; private set; }
        public AssemblyDeviceKind Kind { get; private set; }
        public int? RatedCurrentA { get; private set; }
        public IReadOnlyCollection<AssemblyPortSnapshot> Ports { get { return _ports.Values; } }

        public AssemblyDeviceSnapshot(
            string id,
            string sourceComponentName,
            AssemblyDeviceKind kind,
            int? ratedCurrentA,
            IEnumerable<AssemblyPortSnapshot> ports)
        {
            Id = id;
            SourceComponentName = sourceComponentName;
            Kind = kind;
            RatedCurrentA = ratedCurrentA;
            _ports = (ports ?? Enumerable.Empty<AssemblyPortSnapshot>())
                .ToDictionary(port => port.Name, StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGetPort(string name, out AssemblyPortSnapshot port)
        {
            return _ports.TryGetValue(name, out port);
        }

        public AssemblyPortSnapshot GetRequiredPort(string name)
        {
            AssemblyPortSnapshot port;
            if (!_ports.TryGetValue(name, out port))
                throw new Exception("Missing reference point: " + SourceComponentName + "." + name);

            return port;
        }
    }

    internal sealed class AssemblySnapshot
    {
        public string SourceId { get; private set; }
        public AssemblyDeviceSnapshot Fuse { get; private set; }
        public IReadOnlyList<AssemblyDeviceSnapshot> Breakers { get; private set; }

        public AssemblySnapshot(
            string sourceId,
            AssemblyDeviceSnapshot fuse,
            IEnumerable<AssemblyDeviceSnapshot> breakers)
        {
            SourceId = sourceId;
            Fuse = fuse ?? throw new ArgumentNullException("fuse");
            Breakers = (breakers ?? Enumerable.Empty<AssemblyDeviceSnapshot>()).ToList().AsReadOnly();
        }
    }
}
