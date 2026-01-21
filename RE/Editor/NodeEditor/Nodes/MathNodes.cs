using System;
using System.Collections.Generic;
using System.Text;
using RE.Editor.NodeEditor.Pins;

namespace RE.Editor.NodeEditor.Nodes
{
    public abstract class BinaryMathNode : DataNode
    {
        protected readonly DataNodePin<float> _inputA;
        protected readonly DataNodePin<float> _inputB;
        protected readonly DataNodePin<float> _output;

        protected BinaryMathNode()
        {
            //_inputA = new("A", this);
            //_inputB = new("B", this);
            //_output = new("Result", this);
            InputPins.AddRange(new[] { _inputA, _inputB });
            OutputPins.Add(_output);
        }
    }

    /*
     public abstract class UnaryMathNode : DataNode
       {
           protected readonly DataNodePin<float> _input;
           protected readonly DataNodePin<float> _output;
       
           protected UnaryMathNode()
           {
               _input = new("In", this);
               _output = new("Result", this);
               InputPins.Add(_input);
               OutputPins.Add(_output);
           }
       }
       public class SuьNode : BinaryMathNode
       {
           public override string Name => "+";
           public override void Execute() => _output.Value = _inputA.Value + _inputB.Value;
       }
       public class SubNode : BinaryMathNode
       {
           public override string Name => "-";
           public override void Execute() => _output.Value = _inputA.Value - _inputB.Value;
       }
       
       public class MulNode : BinaryMathNode
       {
           public override string Name => "*";
           public override void Execute() => _output.Value = _inputA.Value * _inputB.Value;
       }
       
       public class DivNode : BinaryMathNode
       {
           public override string Name => "/";
           public override void Execute()
           {
               // Проверка на ноль, чтобы избежать NaN/Infinity в логике
               float b = _inputB.Value;
               _output.Value = b != 0 ? _inputA.Value / b : 0f;
           }
       }
       
       public class ModuloNode : BinaryMathNode
       {
           public override string Name => "Mod";
           public override void Execute() => _output.Value = _inputA.Value % _inputB.Value;
       }
       public class PowerNode : BinaryMathNode
       {
           public override string Name => "Pow";
           public override void Execute() => _output.Value = MathF.Pow(_inputA.Value, _inputB.Value);
       }
       
       public class MinNode : BinaryMathNode
       {
           public override string Name => "Min";
           public override void Execute() => _output.Value = MathF.Min(_inputA.Value, _inputB.Value);
       }
       
       public class MaxNode : BinaryMathNode
       {
           public override string Name => "Max";
           public override void Execute() => _output.Value = MathF.Max(_inputA.Value, _inputB.Value);
       }
       public class AbsNode : UnaryMathNode
       {
           public override string Name => "Abs";
           public override void Execute() => _output.Value = MathF.Abs(_input.Value);
       }
       
       public class SqrtNode : UnaryMathNode
       {
           public override string Name => "Sqrt";
           public override void Execute()
           {
               float val = _input.Value;
               _output.Value = val >= 0 ? MathF.Sqrt(val) : 0f;
           }
       }
       
       public class NegateNode : UnaryMathNode
       {
           public override string Name => "Neg";
           public override void Execute() => _output.Value = -_input.Value;
       }
       public class SinNode : UnaryMathNode
       {
           public override string Name => "Sin";
           public override void Execute() => _output.Value = MathF.Sin(_input.Value);
       }
       
       public class CosNode : UnaryMathNode
       {
           public override string Name => "Cos";
           public override void Execute() => _output.Value = MathF.Cos(_input.Value);
       }
       
       public class TanNode : UnaryMathNode
       {
           public override string Name => "Tan";
           public override void Execute() => _output.Value = MathF.Tan(_input.Value);
       }
       public class RoundNode : UnaryMathNode
       {
           public override string Name => "Round";
           public override void Execute() => _output.Value = MathF.Round(_input.Value);
       }
       
       public class CeilNode : UnaryMathNode
       {
           public override string Name => "Ceil";
           public override void Execute() => _output.Value = MathF.Ceiling(_input.Value);
       }
       
       public class FloorNode : UnaryMathNode
       {
           public override string Name => "Floor";
           public override void Execute() => _output.Value = MathF.Floor(_input.Value);
       }
     */
}
