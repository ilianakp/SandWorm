using System;
using System.Drawing;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;

namespace SandWorm
{
    public class MenuButton : GH_Attr_Widget
    {

        private bool _active;
		private int _buttonHeight = 20;
		private Rectangle _buttonBounds;
		private int _verticalPadding = 8;

		public bool Active
		{
			get
			{
				return _active;
			}
			set
			{
				_active = value;
			}
		}

		public override string Name
		{
			get
			{
				return _name;
			}
			set
			{
				_name = value;
			}
		}

		public event ValueChangeEventHandler ValueChanged;


		public MenuButton(int index, string id) : base(index, id)
		{
			Name = id;
		}

		public override SizeF ComputeMinSize()
		{
			float height = TextRenderer.MeasureText("1", WidgetServer.Instance.SliderValueTagFont).Height;
			return new System.Drawing.SizeF(20f, _buttonHeight + _verticalPadding);
		}

		public override void Layout()
		{
			_buttonBounds = new Rectangle(
				(int)base.CanvasPivot.X, (int)base.CanvasPivot.Y + (int)(_verticalPadding / 2), 
				(int)base.Width, _buttonHeight + _verticalPadding);
		}


		public override void Render(WidgetRenderArgs args)
		{
			Graphics graphics = args.Canvas.Graphics;
			GH_Capsule button;
			var buttonBox = _buttonBounds;
			buttonBox.Height -= _verticalPadding;
			buttonBox.Y += _verticalPadding / 2;

			if (_active)
			{
				button = GH_Capsule.CreateTextCapsule(buttonBox, buttonBox, GH_Palette.Grey, this.Name, 1, 0);
				_active = false;
			}
			else
			{
				button = GH_Capsule.CreateTextCapsule(buttonBox, buttonBox, GH_Palette.Black, this.Name, 1, 0);
			}

			button.Render(graphics, _active, false, false);
			button.Dispose();
		}

		public override void PostUpdateBounds(out float outHeight)
		{
			outHeight = ComputeMinSize().Height;
		}
	}
}