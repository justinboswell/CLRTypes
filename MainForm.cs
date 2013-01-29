using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EmittingCLRTypes
{
	public partial class MainForm : Form
	{
		object TestClassInstance = null;

		public MainForm()
		{
			InitializeComponent();
		}

		private void MainForm_Load(object sender, EventArgs e)
		{
			ReflectedClass.CreateReflectedAssembly();

			List<ReflectedProperty> Properties = new List<ReflectedProperty>();

			IntProperty HealthProperty = new IntProperty("Health");
			Properties.Add(HealthProperty);

			FloatProperty MassProperty = new FloatProperty("Mass");
			MassProperty.Category = "Physics";
			Properties.Add(MassProperty);

			List<string> TeamValues = new List<string>(new string[] {
			    "Humans",
			    "Aliens",
			    "Neutral",
			});
			Properties.Add(new EnumProperty("Team", "ETeam", TeamValues));

			ReflectedClass TestClass = ReflectedClass.Create("GameUnit", Properties);
			ReflectedClass.SaveReflectedAssembly();

			TestClassInstance = TestClass.Construct();
			Debug.WriteLine(TestClassInstance.ToString());

			PropertyInfo[] Members = TestClassInstance.GetType().GetProperties();
			foreach (PropertyInfo Prop in Members)
			{
				Debug.WriteLine("  {0} = {1}", Prop.Name, Prop.GetValue(TestClassInstance, null));
			}

			PropertyGridControl.SelectedObject = TestClassInstance;
		}
	}
}
