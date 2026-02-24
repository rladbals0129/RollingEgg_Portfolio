using RollingEgg.UI;
using UnityEngine;

namespace RollingEgg
{
    public class UI_Message : UI_Popup
    {
        public void OnClickClose()
        {
            var uiManager = UIManager.Instance;
            if (uiManager == null)
            {
                Hide();
                return;
            }

            uiManager.ClosePopup(this);

            
        }
    }
}
