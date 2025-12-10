using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TienLen.Unity.Presentation.Views
{
    public class AvatarView : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image _avatarImage;
        [SerializeField] private TMP_Text _nameText;

        public void SetAvatar(Sprite sprite)
        {
            if (_avatarImage != null && sprite != null)
            {
                _avatarImage.sprite = sprite;
            }
        }

        public void SetName(string name)
        {
            if (_nameText != null)
            {
                _nameText.text = name;
            }
        }
    }
}
