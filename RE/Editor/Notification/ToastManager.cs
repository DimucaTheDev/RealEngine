using System.Numerics;
using Hexa.NET.ImGui;
using RE.Core;
using RE.Core.Audio;

namespace RE.Editor.Notification
{
    public static class ToastManager
    {
        private static readonly List<Toast> Notifications = new();

        internal static ImGuiViewportPtr MainWindowViewport;

        public static void InsertNotification(Toast toast)
        {
            Notifications.Add(toast);
            if (toast.Type == ToastType.Error)
                SoundManager.PlayOneShotEvent("event:/Toast");
        }

        public static void RemoveNotification(int index)
        {
            if (index >= 0 && index < Notifications.Count)
            {
                Notifications.RemoveAt(index);
            }
        }

        public static void RenderNotifications()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 5f);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(43 / 255, 43 / 255, 43 / 255, 100 / 255));

            var vpSize = MainWindowViewport.Size;
            float height = 0;

            for (int i = 0; i < Notifications.Count; i++)
            {
                var currentToast = Notifications[i];
                currentToast.LifeTime += Time.DeltaTime;
                if (currentToast.GetPhase() == ToastPhase.Expired)
                {
                    RemoveNotification(i--);
                    continue;
                }

                var icon = currentToast.GetIcon();
                var title = currentToast.Title;
                var content = currentToast.Content;
                var defaultTitle = currentToast.GetDefaultTitle();
                float elapsed = currentToast.LifeTime;
                float opacity = 1.0f;
                var fadeTime = ToastManagerConfig.FadeInOutTime;
                if (elapsed < fadeTime)
                {
                    opacity = elapsed / fadeTime;
                }
                else if (elapsed > fadeTime + currentToast.DismissTime)
                {
                    opacity = 1.0f - (elapsed - (fadeTime + currentToast.DismissTime)) / fadeTime;
                }
                float waitTime = ToastManagerConfig.FadeInOutTime;

                float targetX = vpSize.X - ToastManagerConfig.PaddingX;
                float offscreenX = vpSize.X + 10f;
                float currentX = targetX;

                var phase = currentToast.GetPhase();
                if (phase == ToastPhase.FadeIn)
                {
                    float progress = elapsed / ToastManagerConfig.FadeInOutTime;
                    float easeOut = 1f - MathF.Pow(1f - progress, 3);
                    currentX = offscreenX - (offscreenX - targetX) * easeOut;
                }
                else if (phase == ToastPhase.FadeOut)
                {
                    float progress = (elapsed - ToastManagerConfig.FadeInOutTime - currentToast.DismissTime) / ToastManagerConfig.FadeInOutTime;
                    float easeIn = MathF.Pow(progress, 3);
                    currentX = targetX + (offscreenX - targetX) * easeIn;
                }

                Vector2 windowPos = new Vector2(MainWindowViewport.Pos.X + currentX, MainWindowViewport.Pos.Y + vpSize.Y - ToastManagerConfig.PaddingY - height);
                ImGui.SetNextWindowBgAlpha(opacity);
                ImGui.SetNextWindowViewport(MainWindowViewport.ID);
                ImGui.SetNextWindowPos(windowPos, ImGuiCond.Always, new Vector2(1.0f, 1.0f));
                ImGui.SetNextWindowSizeConstraints(new Vector2(150, 0), new Vector2(2000, 1000));
                
                ImGui.Begin($"##TOAST{i}", ToastManagerConfig.ToastFlags);
                {
                    ImGui.PushTextWrapPos(vpSize.X / 3.0f);

                    bool wasTitleRendered = false;

                    if (!string.IsNullOrEmpty(icon))
                    {
                        ImGui.TextColored(currentToast.GetIconColor(), icon);
                        wasTitleRendered = true;
                    }

                    var fmt = $"{(Math.Max(0, currentToast.DismissTime - currentToast.LifeTime)):F1}s";
                    if (!string.IsNullOrEmpty(title))
                    {
                        if (!string.IsNullOrEmpty(icon))
                            ImGui.SameLine();
                        ImGui.TextColored(new(1, 1, 1, currentToast.GetFadePercent()), title);
                        wasTitleRendered = true;
                        var textSize = ImGui.CalcTextSize(fmt);
                        ImGui.SameLine();
                        ImGui.SetCursorPosX(ImGui.GetWindowSize().X - textSize.X - 10);
                        ImGui.TextColored(new Vector4(1, 1, 1, currentToast.GetFadePercent() * 0.5f), fmt);
                    }
                    else if (!string.IsNullOrEmpty(defaultTitle))
                    {
                        if (!string.IsNullOrEmpty(icon))
                            ImGui.SameLine();
                        ImGui.TextColored(new(1, 1, 1, currentToast.GetFadePercent()), defaultTitle);
                        wasTitleRendered = true;
                        var textSize = ImGui.CalcTextSize(fmt);
                        ImGui.SameLine();
                        ImGui.SetCursorPosX(ImGui.GetWindowSize().X - textSize.X - 10);
                        ImGui.TextColored(new Vector4(1, 1, 1, currentToast.GetFadePercent() * 0.5f), fmt);
                    }

                    if (wasTitleRendered && !string.IsNullOrEmpty(content))
                    {
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5.0f);

                        if (ToastManagerConfig.UseSeparator)
                        {
                            //ImGui.PushStyleVar(ImGuiStyleVar.SeparatorTextPadding, new Vector2(0));
                            ImGui.Separator();
                            //ImGui.PopStyleVar();
                        }
                    }

                    if (!string.IsNullOrEmpty(content))
                    {
                        ImGui.TextColored(new(1, 1, 1, currentToast.GetFadePercent()), content);
                    }

                    ImGui.PopTextWrapPos();
                }

                height += ImGui.GetWindowHeight() + ToastManagerConfig.PaddingMessageY;

                ImGui.End();
            }

            ImGui.PopStyleVar(1);
            ImGui.PopStyleColor(1);
        }

        public static void RemoveAllNotifications()
        {
            Notifications.Clear();
        }
    }
}