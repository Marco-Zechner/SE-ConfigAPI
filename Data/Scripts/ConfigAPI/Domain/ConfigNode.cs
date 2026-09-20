using System;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public abstract class ConfigNode : IEquatable<ConfigNode>
    {
        public bool Equals(ConfigNode other)
        {
            if (ReferenceEquals(other, null))
                return false;

            return ReferenceEquals(this, other) || EqualsNode(other);
        }

        public override bool Equals(object obj) => Equals(obj as ConfigNode);

        public abstract override int GetHashCode();

        protected abstract bool EqualsNode(ConfigNode other);
    }
}
