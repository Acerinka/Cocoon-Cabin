using UnityEngine;
using UnityEngine.UI;

namespace CocoonPrototype
{
    public enum CocoonButtonAction
    {
        ConfirmRide,
        SelectDestination,
        SelectRideMode,
        ConfirmPayment,
        DeclineRide,
        BaggageYes,
        BaggageNo,
        ResetExperience
    }

    public sealed class CocoonWorldButton : MonoBehaviour
    {
        [SerializeField] private CocoonButtonAction action;
        [SerializeField] private string payload;
        [SerializeField] private CocoonTaxiStateMachine taxiStateMachine;
        [SerializeField] private Graphic tintGraphic;
        [SerializeField] private Color normalColor = new Color(0.08f, 0.11f, 0.13f, 0.95f);
        [SerializeField] private Color hoverColor = new Color(0.1f, 0.45f, 0.62f, 0.98f);

        public void Configure(CocoonTaxiStateMachine stateMachine, CocoonButtonAction buttonAction, string buttonPayload, Graphic graphic)
        {
            taxiStateMachine = stateMachine;
            action = buttonAction;
            payload = buttonPayload;
            tintGraphic = graphic;
            SetHighlighted(false);
        }

        public void SetHighlighted(bool highlighted)
        {
            if (tintGraphic != null)
            {
                tintGraphic.color = highlighted ? hoverColor : normalColor;
            }
        }

        public void Press()
        {
            CocoonDebugLog.Info("DoorUI", "Button press: " + action + (string.IsNullOrEmpty(payload) ? "" : " payload='" + payload + "'") + ".", this);
            if (taxiStateMachine == null)
            {
                CocoonDebugLog.Warn("DoorUI", "Button press ignored because taxiStateMachine is missing.", this);
                return;
            }

            switch (action)
            {
                case CocoonButtonAction.ConfirmRide:
                    taxiStateMachine.ConfirmRide();
                    break;
                case CocoonButtonAction.SelectDestination:
                    taxiStateMachine.SelectDestination(payload);
                    break;
                case CocoonButtonAction.SelectRideMode:
                    taxiStateMachine.SelectRideMode(payload);
                    break;
                case CocoonButtonAction.ConfirmPayment:
                    taxiStateMachine.ConfirmPayment();
                    break;
                case CocoonButtonAction.DeclineRide:
                    taxiStateMachine.DeclineRide();
                    break;
                case CocoonButtonAction.BaggageYes:
                    taxiStateMachine.SelectBaggagePreference(true);
                    break;
                case CocoonButtonAction.BaggageNo:
                    taxiStateMachine.SelectBaggagePreference(false);
                    break;
                case CocoonButtonAction.ResetExperience:
                    taxiStateMachine.ResetExperience();
                    break;
            }
        }
    }
}
