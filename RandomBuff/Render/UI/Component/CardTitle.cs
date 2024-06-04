﻿using RandomBuff.Render.CardRender;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomBuff.Render.UI.Component
{
    internal class CardTitle
    {
        public int flipCounter;
        public int flipDelay;

        public FContainer container;

        public float anchorX = 0.5f;
        public float scale;
        public Vector2 lastPos;
        public Vector2 pos;

        public float spanShrink;
        bool readyForSwitch;
        int handlerCount;
        protected List<SingleCardHandler> currentActiveHandlers = new List<SingleCardHandler>();

        List<string> switchTitleRequest = new List<string>();

        public CardTitle(FContainer container, float scale, Vector2 pos, float spanShrink = 1f, float anchorX = 0.5f, int flipCounter = 20, int flipDelay = 5)
        {
            this.scale = scale;
            lastPos = this.pos = pos;
            this.container = container;
            this.spanShrink = spanShrink;
            this.anchorX = anchorX;
            this.flipCounter = flipCounter;
            this.flipDelay = flipDelay;
        }

        public void Update()
        {
            lastPos = pos;
            readyForSwitch = true;
            for (int i = currentActiveHandlers.Count - 1; i >= 0; i--)
            {
                currentActiveHandlers[i].Update();
                if(i < currentActiveHandlers.Count)
                    readyForSwitch &= currentActiveHandlers[i].ReadyForSwitch;
            }

            if(switchTitleRequest.Count != 0)
            {
                if(currentActiveHandlers.Count == 0)
                    SwitchTitle(switchTitleRequest.Last());
                else if (readyForSwitch)
                {
                    foreach (var handler in currentActiveHandlers)
                        handler.SwitchMode(SingleCardHandler.Mode.FlipOut);
                }
            }
        }

        public void GrafUpdate(float timeStacker)
        {
            for(int i = currentActiveHandlers.Count - 1; i >= 0; i--)
                currentActiveHandlers[i].GrafUpdate(timeStacker);
        }

        void SwitchTitle(string text)
        {
            switchTitleRequest.Clear();
            handlerCount = text.Length;
            for(int i = 0;i < text.Length; i++)
            {
                currentActiveHandlers.Add(new SingleCardHandler(this, text.Substring(i,1), i));
            }
        }

        public void RequestSwitchTitle(string title, bool caspLock = false)
        {
            switchTitleRequest.Add(caspLock ? title.ToUpper() : title);
        }

        public void Destroy()
        {
            for (int i = currentActiveHandlers.Count - 1; i >= 0; i--)
                currentActiveHandlers[i].Destroy();
        }

        protected class SingleCardHandler
        {
            CardTitle title;
            SingleTextCard card;

            int delay;
            int lastCounter;
            int counter;

            Vector2 deltaPos;

            Mode currentMode;

            public bool ReadyForSwitch => (lastCounter == counter) && (counter == title.flipCounter) && currentMode == Mode.FlipIn;

            public SingleCardHandler(CardTitle title, string text, int index)
            {
                this.title = title;
                card = new SingleTextCard(text);
                card.Scale = title.scale;
                title.container.AddChild(card.Container);
                card.CardTexture.shader = Custom.rainWorld.Shaders["MenuText"];
                delay = index * -title.flipDelay;
                deltaPos = new Vector2(CardBasicAssets.RenderTextureSize.x * 0.5f * title.spanShrink * title.scale * 1.1f * (index -(title.handlerCount - 1) / 2f), 0f) + new Vector2(CardBasicAssets.RenderTextureSize.x * 0.5f * title.spanShrink * title.scale * 1.1f * (0.5f - title.anchorX) * title.handlerCount, 0f);

                SwitchMode(Mode.FlipIn);
            }

            public void Update()
            {
                lastCounter = counter;
                if (counter < title.flipCounter)
                    counter++;

                if(currentMode == Mode.FlipIn)
                {
                    //DoNothingCurrently
                }
                else if(currentMode == Mode.FlipOut)
                {
                    if (lastCounter == counter && counter == title.flipCounter)
                        Destroy();
                }
            }

            public void GrafUpdate(float timeStacker)
            {
                float flip = counter / (float)title.flipCounter;
                float lastFlip = lastCounter / (float)title.flipCounter;
                float smoothFlip = Mathf.Clamp01(Mathf.Lerp(lastFlip, flip, timeStacker));

                Vector2 smoothPos = Vector2.Lerp(title.lastPos, title.pos, timeStacker) + deltaPos;

                if(currentMode == Mode.FlipIn)
                {
                    card.Rotation = new Vector3(0, -90 + 90 * Helper.LerpEase(smoothFlip), 0f);
                }
                else
                {
                    card.Rotation = new Vector3(0, 90 * Helper.LerpEase(smoothFlip), 0f);
                }

                card.Position = smoothPos;
            }

            public void Destroy()
            {
                card.Destroy();

                title.currentActiveHandlers.Remove(this);
            }

            public void SwitchMode(Mode mode)
            {
                counter = 0 + delay;
                lastCounter = counter;
                currentMode = mode;
            }

            public enum Mode
            {
                FlipIn,
                FlipOut
            }
        }
    }
}