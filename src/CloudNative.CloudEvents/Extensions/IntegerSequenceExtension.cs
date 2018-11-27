// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.Extensions
{
    public class IntegerSequenceExtension : SequenceExtension
    {
        public IntegerSequenceExtension(int? sequenceValue = null) : base()
        {
            base.SequenceType = "Integer";
            this.Sequence = sequenceValue;
        }

        public new int? Sequence
        {
            get
            {
                var s = base.Sequence;
                if (s != null)
                {
                    return int.Parse(s);
                }        
                return null;
            }
            set
            {
                base.Sequence = value?.ToString();
            }
        }

        public new string SequenceType
        {
            get => base.SequenceType;
        }
    }
}