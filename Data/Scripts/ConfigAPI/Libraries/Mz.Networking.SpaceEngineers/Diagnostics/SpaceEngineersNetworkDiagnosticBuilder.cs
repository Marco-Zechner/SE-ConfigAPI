using System;
using System.Collections.Generic;
using System.Text;

namespace Mz.Networking.SpaceEngineers
{
    internal sealed class SpaceEngineersNetworkDiagnosticData
    {
        public SpaceEngineersNetworkDiagnosticSeverity Severity { get; }

        public string Code { get; }

        public string Message { get; }

        public string PacketPreview { get; }

        public string[] DiscoveredText { get; }

        public SpaceEngineersNetworkDiagnosticData(
            SpaceEngineersNetworkDiagnosticSeverity severity,
            string code,
            string message,
            string packetPreview,
            string[] discoveredText)
        {
            Severity = severity;
            Code = code;
            Message = message;
            PacketPreview = packetPreview;
            DiscoveredText = discoveredText;
        }
    }

    internal static class SpaceEngineersNetworkDiagnosticBuilder
    {
        internal const int MaximumPreviewBytes = 32;
        internal const int MaximumInspectedBytes = 512;
        internal const int MaximumDiscoveredTextCount = 4;
        internal const int MaximumDiscoveredTextLength = 96;
        internal const int MaximumMessageLength = 1024;

        private const int MinimumDiscoveredTextLength = 4;

        private static readonly UTF8Encoding _strictUtf8 = new UTF8Encoding(false, true);

        public static SpaceEngineersNetworkDiagnosticData Build(
            ushort channelId, byte[] serialized, ulong senderPeerId, bool senderIsServer,
            SpaceEngineersNetworkReceiveFailureKind kind, string expectedNetworkId, string observedNetworkId,
            ISpaceEngineersNetworkDiagnosticGateway diagnosticGateway)
        {
            if (serialized == null)
                throw new ArgumentNullException(nameof(serialized));

            SpaceEngineersNetworkDiagnosticSeverity severity = GetSeverity(kind);
            string code = GetCode(kind);
            string preview = BuildPreview(serialized);
            string[] discoveredText = IsConflict(kind) ? DiscoverText(serialized, diagnosticGateway) : Array.Empty<string>();

            string message = BuildMessage(channelId, serialized.Length, senderPeerId, senderIsServer, 
                                          kind, code, expectedNetworkId, observedNetworkId, preview, discoveredText);

            return new SpaceEngineersNetworkDiagnosticData(severity, code, message, preview, discoveredText);
        }

        private static SpaceEngineersNetworkDiagnosticSeverity GetSeverity(SpaceEngineersNetworkReceiveFailureKind kind)
        {
            switch (kind)
            {
                case SpaceEngineersNetworkReceiveFailureKind.ForeignPacket:
                case SpaceEngineersNetworkReceiveFailureKind.NetworkMismatch:
                case SpaceEngineersNetworkReceiveFailureKind.UnsupportedWireVersion:
                case SpaceEngineersNetworkReceiveFailureKind.MalformedWirePacket:
                    return SpaceEngineersNetworkDiagnosticSeverity.Warning;

                case SpaceEngineersNetworkReceiveFailureKind.MalformedOwnPacket:
                case SpaceEngineersNetworkReceiveFailureKind.ProcessingFailure:
                case SpaceEngineersNetworkReceiveFailureKind.HandlerFailure:
                    return SpaceEngineersNetworkDiagnosticSeverity.Error;

                default:
                    throw new ArgumentException("The receive failure kind is outside the supported range.", nameof(kind));
            }
        }

        private static string GetCode(SpaceEngineersNetworkReceiveFailureKind kind)
        {
            switch (kind)
            {
                case SpaceEngineersNetworkReceiveFailureKind.ForeignPacket:
                    return "network.receive.foreign-packet";

                case SpaceEngineersNetworkReceiveFailureKind.NetworkMismatch:
                    return "network.receive.network-mismatch";

                case SpaceEngineersNetworkReceiveFailureKind.UnsupportedWireVersion:
                    return "network.receive.unsupported-wire-version";

                case SpaceEngineersNetworkReceiveFailureKind.MalformedWirePacket:
                    return "network.receive.malformed-wire-packet";

                case SpaceEngineersNetworkReceiveFailureKind.MalformedOwnPacket:
                    return "network.receive.malformed-own-packet";

                case SpaceEngineersNetworkReceiveFailureKind.ProcessingFailure:
                    return "network.receive.processing-failure";

                case SpaceEngineersNetworkReceiveFailureKind.HandlerFailure:
                    return "network.receive.handler-failure";

                default:
                    throw new ArgumentException("The receive failure kind is outside the supported range.", nameof(kind));
            }
        }

        private static bool IsConflict(SpaceEngineersNetworkReceiveFailureKind kind) 
            => kind == SpaceEngineersNetworkReceiveFailureKind.ForeignPacket || kind == SpaceEngineersNetworkReceiveFailureKind.NetworkMismatch;

        private static string BuildPreview(byte[] serialized)
        {
            if (serialized.Length == 0)
                return string.Empty;

            int count = Math.Min(serialized.Length, MaximumPreviewBytes);

            var builder = new StringBuilder(count * 3 + 32);

            for (var index = 0; index < count; index++)
            {
                if (index > 0)
                    builder.Append(' ');

                builder.Append(serialized[index].ToString("X2"));
            }

            if (serialized.Length <= count)
                return builder.ToString();
            
            builder.Append(" ... (+");
            builder.Append(serialized.Length - count);
            builder.Append(" bytes)");

            return builder.ToString();
        }

        private static string[] DiscoverText(byte[] serialized, ISpaceEngineersNetworkDiagnosticGateway diagnosticGateway)
        {
            var discovered = new List<string>();

            if (diagnosticGateway != null && serialized.Length <= MaximumInspectedBytes) 
            {
                try
                {
                    string value;
                    if (diagnosticGateway.TryDeserializeString(serialized, out value)) 
                        AddCandidate(discovered, value);
                }
                catch
                {
                    // ignored
                }
            }

            if (serialized.Length <= MaximumInspectedBytes)
            {
                try
                {
                    AddCandidate(discovered, _strictUtf8.GetString(serialized));
                }
                catch (DecoderFallbackException)
                {
                    // ignored
                }
            }

            AddAsciiCandidates(discovered, serialized);
            AddUtf16Candidates(discovered, serialized, 0);
            AddUtf16Candidates(discovered, serialized, 1);

            return discovered.ToArray();
        }

        private static void AddAsciiCandidates(List<string> discovered, byte[] serialized)
        {
            int inspectedLength = Math.Min(serialized.Length, MaximumInspectedBytes);

            var index = 0;

            while (index < inspectedLength && discovered.Count < MaximumDiscoveredTextCount) {
                while (index < inspectedLength && !IsPrintableAscii(serialized[index]))
                    index++;

                int start = index;

                while (index < inspectedLength && IsPrintableAscii(serialized[index]))
                    index++;

                int length = index - start;

                if (length < MinimumDiscoveredTextLength)
                    continue;

                AddCandidate(discovered, Encoding.ASCII.GetString(serialized, start, length));
            }
        }

        private static void AddUtf16Candidates(List<string> discovered, byte[] serialized, int offset)
        {
            int inspectedLength = Math.Min(serialized.Length, MaximumInspectedBytes);

            int index = offset;

            while (index + 1 < inspectedLength && discovered.Count < MaximumDiscoveredTextCount) {
                while (index + 1 < inspectedLength && !IsPrintableUtf16Pair(serialized[index], serialized[index + 1]))
                    index += 2;

                int start = index;
                var characterCount = 0;

                while (index + 1 < inspectedLength && IsPrintableUtf16Pair(serialized[index], serialized[index + 1])) {
                    characterCount++;
                    index += 2;
                }

                if (characterCount < MinimumDiscoveredTextLength)
                    continue;

                var characters = new char[Math.Min(characterCount, MaximumDiscoveredTextLength)];

                for (var characterIndex = 0; characterIndex < characters.Length; characterIndex++)
                    characters[characterIndex] = (char)serialized[start + characterIndex * 2];

                AddCandidate(discovered, new string(characters));
            }
        }

        private static bool IsPrintableAscii(byte value)
            => value >= 32 && value <= 126;

        private static bool IsPrintableUtf16Pair(byte low, byte high)
            => high == 0 && IsPrintableAscii(low);

        private static void AddCandidate(List<string> discovered, string candidate)
        {
            if (candidate == null || discovered.Count >= MaximumDiscoveredTextCount)
                return;

            string sanitized = Sanitize(candidate);

            if (sanitized.Length < MinimumDiscoveredTextLength || Contains(discovered, sanitized)) {
                return;
            }

            discovered.Add(sanitized);
        }

        private static bool Contains(List<string> discovered, string candidate)
        {
            foreach (string discoveredItem in discovered)
                if (string.Equals(discoveredItem, candidate, StringComparison.Ordinal))
                    return true;

            return false;
        }

        private static string Sanitize(string value)
            => Sanitize(value, MaximumDiscoveredTextLength);

        private static string Sanitize(string value, int maximumLength)
        {
            var builder = new StringBuilder(Math.Min(value.Length, maximumLength));

            var pendingSpace = false;

            for (var index = 0; index < value.Length && builder.Length < maximumLength; index++) 
            {
                char character = value[index];

                if (char.IsControl(character) || char.IsWhiteSpace(character)) 
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;

                    if (builder.Length >= maximumLength)
                        break;
                }

                builder.Append(character);
            }

            return builder.ToString().Trim();
        }

        private static string BuildMessage(
            ushort channelId, int packetLength, ulong senderPeerId, bool senderIsServer, SpaceEngineersNetworkReceiveFailureKind kind, 
            string code, string expectedNetworkId, string observedNetworkId, string preview, string[] discoveredText)
        {
            var builder = new StringBuilder();

            builder.Append('[');
            builder.Append(code);
            builder.Append("] Rejected network packet; kind=");
            builder.Append(kind);
            builder.Append("; channel=");
            builder.Append(channelId);
            builder.Append("; sender=");
            builder.Append(senderPeerId);
            builder.Append("; senderIsServer=");
            builder.Append(senderIsServer ? "true" : "false");
            builder.Append("; packetBytes=");
            builder.Append(packetLength);
            AppendValue(builder, "expectedNetworkId", expectedNetworkId);
            AppendValue(builder, "observedNetworkId", observedNetworkId);

            if (preview.Length > 0)
                AppendValue(builder, "preview", preview);

            if (discoveredText.Length > 0)
            {
                builder.Append("; discoveredText=\"");

                for (var index = 0; index < discoveredText.Length; index++) 
                {
                    if (index > 0)
                        builder.Append(" | ");

                    builder.Append(Escape(discoveredText[index]));
                }

                builder.Append('"');
            }

            if (builder.Length <= MaximumMessageLength)
                return builder.ToString();

            return builder.ToString(0, MaximumMessageLength - 3) + "...";
        }

        private static void AppendValue(StringBuilder builder, string name, string value)
        {
            builder.Append("; ");
            builder.Append(name);
            builder.Append('=');

            if (value == null)
            {
                builder.Append("<none>");
                return;
            }

            builder.Append('"');
            builder.Append(Escape(value));
            builder.Append('"');
        }

        private static string Escape(string value)
            => Sanitize(value, MaximumMessageLength).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
