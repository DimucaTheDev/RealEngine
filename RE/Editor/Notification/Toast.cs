using System.Numerics;
using RE.Utils;

namespace RE.Editor.Notification
{
    /// <summary>
    /// Основной класс уведомления
    /// </summary>
    public class Toast
    {
        public ToastType Type { get; set; }
        public string Title { get; set; }
        public string Content { get; private set; }
        public float DismissTime { get; }

        public float LifeTime = 0;

        public Toast(ToastType type, float dismissTime = 4)
        {
            Type = type;
            DismissTime = dismissTime;
        }

        public Toast(ToastType type, string content, float dismissTime = 4)
            : this(type, dismissTime)
        {
            Content = content;
        }

        public Toast(ToastType type, string content, string title, float dismissTime = 4)
            : this(type, dismissTime)
        {
            Content = content;
            Title = title;
        }

        public string GetDefaultTitle()
        {
            if (!string.IsNullOrEmpty(Title))
                return Title;

            return Type switch
            {
                ToastType.Success => "Success",
                ToastType.Warning => "Warning",
                ToastType.Error => "Error",
                ToastType.Info => "Info",
                _ => string.Empty
            };
        }

        public Vector4 GetIconColor()
        {
            return Type switch
            {
                ToastType.Success => new Vector4(0, 1, 0, 1),
                ToastType.Warning => new Vector4(1, 1, 0, 1),
                ToastType.Error => new Vector4(1, 0, 0, 1),
                ToastType.Info => new Vector4(0, 0.615f, 1, 1),
                _ => new Vector4(1, 1, 1, GetFadePercent())
            };
        }

        public string? GetIcon()
        {
            return Type switch
            {
                ToastType.Success => IconFont.CheckCircle,
                ToastType.Warning => IconFont.ExclamationTriangle,
                ToastType.Error => IconFont.TimesCircle,
                ToastType.Info => IconFont.InfoCircle,
                _ => null
            };
        }

        public ToastPhase GetPhase()
        {
            float elapsed = LifeTime;

            if (elapsed > ToastManagerConfig.FadeInOutTime + DismissTime + ToastManagerConfig.FadeInOutTime)
            {
                return ToastPhase.Expired;
            }

            if (elapsed > ToastManagerConfig.FadeInOutTime + DismissTime)
            {
                return ToastPhase.FadeOut;
            }

            if (elapsed > ToastManagerConfig.FadeInOutTime)
            {
                return ToastPhase.Wait;
            }

            return ToastPhase.FadeIn;
        }

        public float GetFadePercent()
        {
            var phase = GetPhase();

            if (phase == ToastPhase.FadeIn)
            {
                return ((float)LifeTime / ToastManagerConfig.FadeInOutTime) * ToastManagerConfig.Opacity;
            }
            else if (phase == ToastPhase.FadeOut)
            {
                return (1.0f - ((float)LifeTime - ToastManagerConfig.FadeInOutTime - DismissTime) / ToastManagerConfig.FadeInOutTime) * ToastManagerConfig.Opacity;
            }

            return ToastManagerConfig.Opacity;
        }
    }
}