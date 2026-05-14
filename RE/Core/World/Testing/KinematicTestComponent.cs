using System.Text.Json.Nodes;
using BulletSharp;
using BulletSharp.Math;
using OpenTK.Windowing.Common;
using RE.Core.Scripting.Attributes;
using RE.Core.World.Components.Physics;
using SixLabors.ImageSharp.Processing.Processors;

namespace RE.Core.World.Testing;

[RequiresComponent(typeof(RigidBodyComponent))]
public class KinematicTestComponent : Component
{ 
    private RigidBody _body;
    private Vector3 _previousPosition;
    public override void Start()
    {
        GetComponent<RigidBodyComponent>()!.Mass = 0;
        _body = GetComponent<RigidBodyComponent>()!.RigidBody;
        // Указываем, что объект управляется пользователем, а не физикой
        
        _body.CollisionFlags |= CollisionFlags.KinematicObject;
        _body.ActivationState = ActivationState.DisableDeactivation;
    }

    public override void Update(FrameEventArgs args)
    {
        float dt = (float)args.Time;
        if (dt <= 0) return; // Защита от деления на ноль

        // 1. Вычисляем целевую позицию
        float y = 10 * (MathF.Sin(Time.ElapsedTime) / 2) + 5;
        Vector3 newPosition = new Vector3(15, y, 0);
        
        // 2. Вычисляем линейную скорость: (НоваяПоз - СтараяПоз) / ВремяКадра
        Vector3 velocity = (newPosition - _previousPosition) / dt;
        
        // 3. Передаем скорость в Bullet. Это ГЛАВНОЕ исправление.
        _body.LinearVelocity = velocity;

        // 4. Телепортируем объект в новую точку (теперь это "честная" телепортация со скоростью)
        Matrix transform = Matrix.Translation(newPosition);
        _body.WorldTransform = transform;

        // Обновляем позицию для следующего кадра
        _previousPosition = newPosition;
    }

    /// <inheritdoc />
    public override JsonNode GetSaveData()
    {
        return new JsonObject();
    }
}