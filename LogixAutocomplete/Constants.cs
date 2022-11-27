using FrooxEngine;
using FrooxEngine.LogiX;
using FrooxEngine.LogiX.Actions;
using FrooxEngine.LogiX.Input;
using FrooxEngine.LogiX.Math;
using FrooxEngine.LogiX.Network;
using FrooxEngine.LogiX.Operators;
using FrooxEngine.LogiX.ProgramFlow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogixAutocomplete
{
    public enum IOType
    {
        ValueInput,
        ValueOutput,
        ImpulseInput,
        ImpulseOutput
    }
    public struct IOPair
    {
        public IOType ioType;
        public Type wireType;

        public IOPair(IOType ioType, Type wireType)
        {
            this.ioType = ioType;
            this.wireType = wireType;
        }
    }
    public class Constants
    {
        [Obsolete("use LogixHelper.GetOutputContentType instead", true)]
        public static Type GetTypeForOutput(IWorldElement output)
        {
            if (output is IOutputElement el) return el.OutputType;
            if (output is ISyncRef syncRef) return syncRef.TargetType;
            if (output is IField field) return field.ValueType;
            return output.GetType();
        }
        public static Dictionary<IOPair, Type[]> nodeLookup = new Dictionary<IOPair, Type[]>
        {
            { new(IOType.ValueInput, typeof(float)), new Type[]
                {
                    typeof(SmoothLerp_Float), typeof(Add_Float), typeof(Abs_Float), typeof(PlusMinus_Float)
                }
            },
            { new(IOType.ValueOutput, typeof(float)), new Type[]
                {
                    typeof(SmoothLerp_Float), typeof(Add_Float), typeof(Abs_Float), typeof(PlusMinus_Float)
                }
            },
            { new(IOType.ImpulseInput, typeof(Action)), new Type[]
                {
                    typeof(FireOnTrue), typeof(ForNode), typeof(ElapsedTimeNode), typeof(UpdatesDelayNode)
                }
            },
            { new(IOType.ImpulseOutput, typeof(Action)), new Type[]
                {
                    typeof(ForNode), typeof(BooleanToggle), typeof(GET_String), typeof(WriteValueNode<BaseX.dummy>)
                }
            }
        };
    }
}
