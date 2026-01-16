namespace ServiceSiteScheduling.Utilities
{
    class FrozenBitSet : BitSet
    {
        public static FrozenBitSet NotAllowed()
        {
            throw new NotSupportedException("write operations not allowed for FrozenBitSet");
        }

        public FrozenBitSet(BitSet b)
            : base(b) { }

        public FrozenBitSet(int length)
            : base(length) { }

        public override bool this[int i]
        {
            set { NotAllowed(); }
        }

        public override FrozenBitSet And(BitSet b) => NotAllowed();

        public override FrozenBitSet Or(BitSet b) => NotAllowed();

        public override FrozenBitSet Or(ulong value, int size, int offset) => NotAllowed();

        public override FrozenBitSet Xor(BitSet b) => NotAllowed();

        public override FrozenBitSet Exclude(BitSet b) => NotAllowed();

        public override FrozenBitSet Not() => NotAllowed();

        public override FrozenBitSet Clear() => NotAllowed();

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 29;
                for (int i = 0; i < this.elements.Length; i++)
                    hash = hash * 486187739 + this.elements[i].GetHashCode();
                return hash;
            }
        }
    }
}
