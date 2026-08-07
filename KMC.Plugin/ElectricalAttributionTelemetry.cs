using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace KMC.Plugin
{
    internal static class ElectricalAttributionTelemetry
    {
        private const string ElectricCharge =
            "ElectricCharge";

        public static string BuildEncodedPayload(
            Vessel vessel)
        {
            if (vessel == null ||
                vessel.parts == null)
            {
                return
                    string.Empty;
            }

            List<Entry> entries =
                new List<Entry>();

            foreach (Part part in vessel.parts)
            {
                if (part == null ||
                    part.Modules == null)
                {
                    continue;
                }

                foreach (PartModule module in part.Modules)
                {
                    if (module == null)
                    {
                        continue;
                    }

                    try
                    {
                        AnalyzeModule(
                            part,
                            module,
                            entries);
                    }
                    catch
                    {
                        // Attribution must never interrupt core telemetry.
                    }
                }
            }

            if (entries.Count == 0)
            {
                return
                    string.Empty;
            }

            StringBuilder plain =
                new StringBuilder();

            for (int index = 0;
                 index < entries.Count;
                 index++)
            {
                if (index > 0)
                {
                    plain.Append(';');
                }

                Entry entry =
                    entries[index];

                AppendField(
                    plain,
                    entry.Kind);

                plain.Append('~');

                AppendField(
                    plain,
                    entry.PartId.ToString(
                        CultureInfo.InvariantCulture));

                plain.Append('~');

                AppendField(
                    plain,
                    entry.Category);

                plain.Append('~');

                AppendField(
                    plain,
                    entry.Evidence);

                plain.Append('~');

                AppendField(
                    plain,
                    entry.CurrentKnown
                        ? "1"
                        : "0");

                plain.Append('~');

                AppendField(
                    plain,
                    entry.CurrentRate.ToString(
                        "R",
                        CultureInfo.InvariantCulture));

                plain.Append('~');

                AppendField(
                    plain,
                    entry.MaximumKnown
                        ? "1"
                        : "0");

                plain.Append('~');

                AppendField(
                    plain,
                    entry.MaximumRate.ToString(
                        "R",
                        CultureInfo.InvariantCulture));

                plain.Append('~');

                AppendField(
                    plain,
                    entry.Enabled
                        ? "1"
                        : "0");

                plain.Append('~');

                AppendField(
                    plain,
                    entry.ActiveKnown
                        ? "1"
                        : "0");

                plain.Append('~');

                AppendField(
                    plain,
                    entry.Active
                        ? "1"
                        : "0");

                plain.Append('~');

                AppendField(
                    plain,
                    entry.PartTitle);
            }

            byte[] bytes =
                Encoding.UTF8.GetBytes(
                    plain.ToString());

            return
                Convert.ToBase64String(
                    bytes);
        }

        private static void AnalyzeModule(
            Part part,
            PartModule module,
            IList<Entry> destination)
        {
            string typeName =
                module.GetType().Name ??
                string.Empty;

            string fullTypeName =
                module.GetType().FullName ??
                typeName;

            bool enabled =
                SafeIsEnabled(
                    module);

            bool active;
            bool activeKnown =
                TryReadBoolean(
                    module,
                    new[]
                    {
                        "IsActivated",
                        "isActive",
                        "IsActive",
                        "isRunning",
                        "IsRunning",
                        "generatorIsActive",
                        "ConverterIsActive",
                        "isDeployed"
                    },
                    out active);

            if (ContainsIgnoreCase(
                    fullTypeName,
                    "SolarPanel"))
            {
                AddSolarProducer(
                    part,
                    module,
                    destination,
                    enabled,
                    activeKnown,
                    active);
            }

            ResourceRate output =
                FindModuleElectricChargeRate(
                    module,
                    false);

            if (output.Found &&
                IsKnownProducerType(
                    fullTypeName))
            {
                AddDeclaredProducer(
                    part,
                    fullTypeName,
                    output.Rate,
                    destination,
                    enabled,
                    activeKnown,
                    active);
            }

            ResourceRate input =
                FindModuleElectricChargeRate(
                    module,
                    true);

            if (input.Found)
            {
                AddDeclaredConsumer(
                    part,
                    fullTypeName,
                    input.Rate,
                    destination,
                    enabled,
                    activeKnown,
                    active);
            }
            else if (ContainsIgnoreCase(
                         fullTypeName,
                         "DataTransmitter"))
            {
                ResourceRate transmitterRate =
                    FindDataTransmitterRate(
                        module);

                if (transmitterRate.Found)
                {
                    AddDeclaredConsumer(
                        part,
                        fullTypeName,
                        transmitterRate.Rate,
                        destination,
                        enabled,
                        activeKnown,
                        active);
                }
            }
        }

        private static void AddSolarProducer(
            Part part,
            PartModule module,
            IList<Entry> destination,
            bool enabled,
            bool activeKnown,
            bool active)
        {
            double currentRate;
            bool currentKnown =
                TryReadDouble(
                    module,
                    new[]
                    {
                        "flowRate",
                        "FlowRate",
                        "currentRate",
                        "CurrentRate"
                    },
                    out currentRate);

            double maximumRate;
            bool maximumKnown =
                TryReadDouble(
                    module,
                    new[]
                    {
                        "chargeRate",
                        "ChargeRate",
                        "maxRate",
                        "MaxRate"
                    },
                    out maximumRate);

            if (!currentKnown &&
                !maximumKnown)
            {
                return;
            }

            Entry entry =
                NewEntry(
                    part,
                    "P",
                    "Solar",
                    currentKnown
                        ? "MeasuredCurrent"
                        : "DeclaredMaximum");

            entry.Enabled =
                enabled;

            entry.ActiveKnown =
                activeKnown;

            entry.Active =
                active;

            entry.CurrentKnown =
                currentKnown;

            entry.CurrentRate =
                currentKnown
                    ? Math.Max(
                        0.0,
                        currentRate)
                    : 0.0;

            entry.MaximumKnown =
                maximumKnown;

            entry.MaximumRate =
                maximumKnown
                    ? Math.Max(
                        0.0,
                        maximumRate)
                    : 0.0;

            destination.Add(
                entry);
        }

        private static void AddDeclaredProducer(
            Part part,
            string typeName,
            double configuredRate,
            IList<Entry> destination,
            bool enabled,
            bool activeKnown,
            bool active)
        {
            string category =
                ClassifyProducer(
                    typeName);

            Entry entry =
                NewEntry(
                    part,
                    "P",
                    category,
                    activeKnown
                        ? "DeclaredActive"
                        : "DeclaredMaximum");

            entry.Enabled =
                enabled;

            entry.ActiveKnown =
                activeKnown;

            entry.Active =
                active;

            entry.MaximumKnown =
                true;

            entry.MaximumRate =
                Math.Max(
                    0.0,
                    configuredRate);

            if (activeKnown)
            {
                entry.CurrentKnown =
                    true;

                entry.CurrentRate =
                    enabled &&
                    active
                        ? entry.MaximumRate
                        : 0.0;
            }

            destination.Add(
                entry);
        }

        private static void AddDeclaredConsumer(
            Part part,
            string typeName,
            double configuredRate,
            IList<Entry> destination,
            bool enabled,
            bool activeKnown,
            bool active)
        {
            if (configuredRate <= 0.0)
            {
                return;
            }

            string category =
                ClassifyConsumer(
                    typeName);

            Entry entry =
                NewEntry(
                    part,
                    "C",
                    category,
                    activeKnown
                        ? "DeclaredActive"
                        : "DeclaredMaximum");

            entry.Enabled =
                enabled;

            entry.ActiveKnown =
                activeKnown;

            entry.Active =
                active;

            entry.MaximumKnown =
                true;

            entry.MaximumRate =
                Math.Max(
                    0.0,
                    configuredRate);

            /*
             * Only call a declared recipe "current" when the module exposes
             * a usable active state. Otherwise retain it as maximum/potential
             * demand. This avoids presenting configured resource recipes as
             * measured power draw.
             */
            if (activeKnown)
            {
                entry.CurrentKnown =
                    true;

                entry.CurrentRate =
                    enabled &&
                    active
                        ? entry.MaximumRate
                        : 0.0;
            }

            destination.Add(
                entry);
        }

        private static Entry NewEntry(
            Part part,
            string kind,
            string category,
            string evidence)
        {
            return
                new Entry
                {
                    Kind =
                        kind,

                    PartId =
                        SafePartId(
                            part),

                    PartTitle =
                        SafeText(
                            part != null
                                ? part.partInfo != null
                                    ? part.partInfo.title
                                    : part.name
                                : string.Empty),

                    Category =
                        category,

                    Evidence =
                        evidence
                };
        }

        private static uint SafePartId(
            Part part)
        {
            try
            {
                return
                    part != null
                        ? part.flightID
                        : 0;
            }
            catch
            {
                return
                    0;
            }
        }

        private static string ClassifyProducer(
            string typeName)
        {
            if (ContainsIgnoreCase(
                    typeName,
                    "FuelCell"))
            {
                return
                    "FuelCell";
            }

            if (ContainsIgnoreCase(
                    typeName,
                    "Alternator"))
            {
                return
                    "Alternator";
            }

            if (ContainsIgnoreCase(
                    typeName,
                    "Generator"))
            {
                return
                    "Generator";
            }

            if (ContainsIgnoreCase(
                    typeName,
                    "Converter"))
            {
                return
                    "Converter";
            }

            return
                "OtherProducer";
        }

        private static string ClassifyConsumer(
            string typeName)
        {
            if (ContainsIgnoreCase(
                    typeName,
                    "ReactionWheel"))
            {
                return
                    "AttitudeControl";
            }

            if (ContainsIgnoreCase(
                    typeName,
                    "Command"))
            {
                return
                    "Command";
            }

            if (ContainsIgnoreCase(
                    typeName,
                    "DataTransmitter") ||
                ContainsIgnoreCase(
                    typeName,
                    "Antenna"))
            {
                return
                    "Communication";
            }

            if (ContainsIgnoreCase(
                    typeName,
                    "Science") ||
                ContainsIgnoreCase(
                    typeName,
                    "Laboratory") ||
                ContainsIgnoreCase(
                    typeName,
                    "Lab"))
            {
                return
                    "Science";
            }

            if (ContainsIgnoreCase(
                    typeName,
                    "Converter") ||
                ContainsIgnoreCase(
                    typeName,
                    "Harvester"))
            {
                return
                    "Utility";
            }

            if (ContainsIgnoreCase(
                    typeName,
                    "Engine"))
            {
                return
                    "Propulsion";
            }

            return
                "OtherConsumer";
        }

        private static bool IsKnownProducerType(
            string typeName)
        {
            return
                ContainsIgnoreCase(
                    typeName,
                    "Generator") ||
                ContainsIgnoreCase(
                    typeName,
                    "Converter") ||
                ContainsIgnoreCase(
                    typeName,
                    "Alternator") ||
                ContainsIgnoreCase(
                    typeName,
                    "FuelCell");
        }

        private static ResourceRate FindModuleElectricChargeRate(
            PartModule module,
            bool input)
        {
            string[] directNames =
                input
                    ? new[]
                    {
                        "inputList",
                        "InputList",
                        "inputResources",
                        "InputResources",
                        "Inputs"
                    }
                    : new[]
                    {
                        "outputList",
                        "OutputList",
                        "outputResources",
                        "OutputResources",
                        "Outputs"
                    };

            ResourceRate direct =
                FindElectricChargeRate(
                    module,
                    directNames);

            if (direct.Found)
            {
                return direct;
            }

            object handler;

            if (!TryReadMember(
                    module,
                    new[]
                    {
                        "resHandler",
                        "ResHandler",
                        "resourceHandler",
                        "ResourceHandler"
                    },
                    out handler) ||
                handler == null)
            {
                return new ResourceRate();
            }

            string[] handlerNames =
                input
                    ? new[]
                    {
                        "inputResources",
                        "InputResources",
                        "inputList",
                        "InputList",
                        "Inputs"
                    }
                    : new[]
                    {
                        "outputResources",
                        "OutputResources",
                        "outputList",
                        "OutputList",
                        "Outputs"
                    };

            return
                FindElectricChargeRate(
                    handler,
                    handlerNames);
        }

        private static ResourceRate FindDataTransmitterRate(
            PartModule module)
        {
            double packetCost;
            double packetInterval;

            bool hasCost =
                TryReadDouble(
                    module,
                    new[]
                    {
                        "packetResourceCost",
                        "PacketResourceCost",
                        "DataResourceCost",
                        "dataResourceCost"
                    },
                    out packetCost);

            bool hasInterval =
                TryReadDouble(
                    module,
                    new[]
                    {
                        "packetInterval",
                        "PacketInterval"
                    },
                    out packetInterval);

            if (!hasCost ||
                !hasInterval ||
                packetCost <= 0.0 ||
                packetInterval <= 0.0)
            {
                return new ResourceRate();
            }

            return
                new ResourceRate
                {
                    Found = true,
                    Rate = packetCost / packetInterval
                };
        }

        private static ResourceRate FindElectricChargeRate(
            object instance,
            IList<string> memberNames)
        {
            object raw;

            if (!TryReadMember(
                    instance,
                    memberNames,
                    out raw) ||
                raw == null)
            {
                return
                    new ResourceRate();
            }

            IEnumerable enumerable =
                raw as IEnumerable;

            if (enumerable == null)
            {
                return
                    new ResourceRate();
            }

            foreach (object item in enumerable)
            {
                if (item == null)
                {
                    continue;
                }

                string name;

                if (!TryReadString(
                        item,
                        new[]
                        {
                            "ResourceName",
                            "resourceName",
                            "Name",
                            "name"
                        },
                        out name))
                {
                    continue;
                }

                if (!string.Equals(
                        name,
                        ElectricCharge,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                double rate;

                if (TryReadDouble(
                        item,
                        new[]
                        {
                            "Ratio",
                            "ratio",
                            "Rate",
                            "rate"
                        },
                        out rate))
                {
                    return
                        new ResourceRate
                        {
                            Found =
                                true,

                            Rate =
                                Math.Max(
                                    0.0,
                                    rate)
                        };
                }
            }

            return
                new ResourceRate();
        }

        private static bool SafeIsEnabled(
            PartModule module)
        {
            try
            {
                return
                    module.isEnabled;
            }
            catch
            {
                return
                    true;
            }
        }

        private static bool TryReadBoolean(
            object instance,
            IList<string> names,
            out bool value)
        {
            value =
                false;

            object raw;

            if (!TryReadMember(
                    instance,
                    names,
                    out raw) ||
                raw == null)
            {
                return
                    false;
            }

            if (raw is bool)
            {
                value =
                    (bool)raw;

                return
                    true;
            }

            return
                bool.TryParse(
                    raw.ToString(),
                    out value);
        }

        private static bool TryReadDouble(
            object instance,
            IList<string> names,
            out double value)
        {
            value =
                0.0;

            object raw;

            if (!TryReadMember(
                    instance,
                    names,
                    out raw) ||
                raw == null)
            {
                return
                    false;
            }

            try
            {
                value =
                    Convert.ToDouble(
                        raw,
                        CultureInfo.InvariantCulture);

                return
                    !double.IsNaN(
                        value) &&
                    !double.IsInfinity(
                        value);
            }
            catch
            {
                value =
                    0.0;

                return
                    false;
            }
        }

        private static bool TryReadString(
            object instance,
            IList<string> names,
            out string value)
        {
            value =
                string.Empty;

            object raw;

            if (!TryReadMember(
                    instance,
                    names,
                    out raw) ||
                raw == null)
            {
                return
                    false;
            }

            value =
                raw.ToString() ??
                string.Empty;

            return
                !string.IsNullOrEmpty(
                    value);
        }

        private static bool TryReadMember(
            object instance,
            IList<string> names,
            out object value)
        {
            value =
                null;

            if (instance == null)
            {
                return
                    false;
            }

            Type type =
                instance.GetType();

            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            for (int index = 0;
                 index < names.Count;
                 index++)
            {
                string name =
                    names[index];

                try
                {
                    PropertyInfo property =
                        type.GetProperty(
                            name,
                            flags);

                    if (property != null &&
                        property.GetIndexParameters().Length == 0)
                    {
                        value =
                            property.GetValue(
                                instance,
                                null);

                        return
                            true;
                    }
                }
                catch
                {
                }

                try
                {
                    FieldInfo field =
                        type.GetField(
                            name,
                            flags);

                    if (field != null)
                    {
                        value =
                            field.GetValue(
                                instance);

                        return
                            true;
                    }
                }
                catch
                {
                }
            }

            return
                false;
        }

        private static bool ContainsIgnoreCase(
            string value,
            string fragment)
        {
            return
                !string.IsNullOrEmpty(
                    value) &&
                value.IndexOf(
                    fragment,
                    StringComparison.OrdinalIgnoreCase) >=
                0;
        }

        private static void AppendField(
            StringBuilder builder,
            string value)
        {
            builder.Append(
                SafeText(
                    value));
        }

        private static string SafeText(
            string value)
        {
            if (string.IsNullOrEmpty(
                    value))
            {
                return
                    string.Empty;
            }

            return
                value
                    .Replace(
                        "~",
                        " ")
                    .Replace(
                        ";",
                        " ")
                    .Replace(
                        "|",
                        " ")
                    .Replace(
                        "\r",
                        " ")
                    .Replace(
                        "\n",
                        " ");
        }

        private sealed class Entry
        {
            public string Kind;
            public uint PartId;
            public string Category;
            public string Evidence;
            public bool CurrentKnown;
            public double CurrentRate;
            public bool MaximumKnown;
            public double MaximumRate;
            public bool Enabled;
            public bool ActiveKnown;
            public bool Active;
            public string PartTitle;
        }

        private struct ResourceRate
        {
            public bool Found;
            public double Rate;
        }
    }
}
