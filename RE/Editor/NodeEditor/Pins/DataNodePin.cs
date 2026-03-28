using System.Collections;
using Hexa.NET.ImGui;
using OpenTK.Mathematics;
using RE.Utils;

namespace RE.Editor.NodeEditor.Pins
{
    public abstract class NodePin
    {
        protected NodePin(string name, Type valueType)
        {
            Id = NodeManager.ObtainNodePinId(this);
            Name = name;
            ValueType = valueType;
        }

        public static Dictionary<Type, uint> PinColors = new() {
            { typeof(void), Color4.White.ToImGuiColor() },
            { typeof(int), Color4.DeepSkyBlue.ToImGuiColor() },
            { typeof(float), Color4.SkyBlue.ToImGuiColor() },
            { typeof(double), Color4.DodgerBlue.ToImGuiColor() },
            { typeof(long), Color4.SteelBlue.ToImGuiColor() },
            { typeof(string), Color4.DeepPink.ToImGuiColor() },
            { typeof(char), Color4.Orange.ToImGuiColor() },
            { typeof(bool), Color4.LimeGreen.ToImGuiColor() },
            { typeof(object), Color4.MediumPurple.ToImGuiColor() },
            { typeof(Guid), Color4.Plum.ToImGuiColor() },
            { typeof(DateTime), Color4.SandyBrown.ToImGuiColor() },
            { typeof(System.Numerics.Vector2), Color4.MediumSpringGreen.ToImGuiColor() },
            { typeof(System.Numerics.Vector3), Color4.SpringGreen.ToImGuiColor() }
        };

        public Node Owner => NodeManager.FindNodeByPin(this);
        public int Id { get; }
        public string Name { get; set; }
        public Type ValueType { get; set; }
        public abstract object? BoxedValue { get; set; }
        public bool MultipleInputs => ValueType == typeof(void) || (ValueType.IsAssignableTo(typeof(IEnumerable)) || ValueType.IsArray) &&
                                      ValueType != typeof(string);

        public IEnumerable<NodePin> GetLinkedPins()
        {
            return NodeManager.GetLinkedPins(this);
        }

        public virtual float GetRequiredWidth()
        {
            var style = ImGui.GetStyle();
            return ImGui.CalcTextSize(Name).X
                   + style.FramePadding.X * 2
                   + style.ItemInnerSpacing.X;
        }

        public virtual void RenderPin()
        {
            ImGui.Text(Name);
        }

        public virtual void FinalizeValue() { }
        public abstract void Reset();
        public abstract void TakeValueFrom(NodePin link);
        public virtual void CopyLocalValue() { }
    }

    public class DataNodePin<T> : NodePin
    {
        public T? Value { get; set; }
        public T? LocalValue { get; set; }
        public List<T?> Values { get; } = new();

        private List<object> _accumulator = new();
        private bool _isElementTypeArray;
        private Type _elementType;

        public DataNodePin(string name) : base(name, typeof(T))
        {
            ValueType = typeof(T);
            _isElementTypeArray = ValueType.IsArray;
            if (_isElementTypeArray)
            {
                _elementType = ValueType.GetElementType()!;
            }
        }

        public override object? BoxedValue
        {
            get => Value;
            set => Value = (T?)value;
        }

        public override void Reset()
        {
            _accumulator.Clear();
            Values.Clear();
            Value = default;
        }

        public override void CopyLocalValue()
        {
            Value = LocalValue;
        }

        public override void TakeValueFrom(NodePin link)
        {
            if (_isElementTypeArray)
            {
                var incomingType = link.BoxedValue.GetType();

                if (incomingType == ValueType) // T[] -> T[]
                {
                    Array incomingArray = (Array)link.BoxedValue;
                    foreach (var item in incomingArray)
                    {
                        _accumulator.Add(item);
                    }
                }
                else if (_elementType.IsAssignableFrom(incomingType)) // T ... T -> T[]
                {
                    _accumulator.Add(link.BoxedValue);
                }
            }
            else
            {
                Value = typeof(T) == typeof(string) ? (T?)(object?)link.BoxedValue?.ToString() : (T?)link.BoxedValue;
            }
        }
        public override void FinalizeValue()
        {
            if (_isElementTypeArray)
            {
                Array newArray = Array.CreateInstance(_elementType, _accumulator.Count);
                for (int i = 0; i < _accumulator.Count; i++)
                {
                    newArray.SetValue(_accumulator[i], i);
                }
                Value = (T)(object)newArray;
            }
        }
        public override string? ToString()
        {
            return Value?.ToString();
        }
    }
}