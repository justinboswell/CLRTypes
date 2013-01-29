
// Copyright (c) 2012 Justin Boswell <justin.boswell@gmail.com>

// Permission is hereby granted, free of charge, to any person obtaining a copy 
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights 
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all 
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A 
// PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection.Emit;
using System.Reflection;
using System.Diagnostics;
using System.Collections;

public abstract class ReflectedProperty
{
	public enum ReflectedType
	{
		INT,
		FLOAT,
		BOOL,
		ENUM,
		STRING,
		ARRAY,
		CLASS,
	};

	public ReflectedType Type
	{
		get;
		protected set;
	}
	
	public int ArrayDim = 1;

	public string Name
	{
		get;
		protected set;
	}

	protected string FieldName
	{
		get { return "_" + Name; }
	}

	public string Category
	{
		get;
		set;
	}

	public bool bIsArray
	{
		get { return (Type == ReflectedType.ARRAY) || (ArrayDim > 1); }
	}

	public abstract object DefaultValue { get; }
	public abstract Type CLRType { get; }

	public ReflectedProperty(string InName)
	{
		Name = InName;
		Category = "Misc";
	}

    public void Emit(TypeBuilder Builder)
    {
        if (CLRType != null)
        {
            try
			{
				// see: http://msdn.microsoft.com/en-us/library/System.Reflection.Emit.PropertyBuilder.aspx for more info
				Type StorageType = (Type != ReflectedType.ARRAY) ? CLRType.MakeArrayType() : CLRType;
				Type PropertyType = (ArrayDim > 1) ? CLRType.MakeArrayType() : CLRType;

				// Add the private field, named _<Name>
				FieldBuilder Field = Builder.DefineField(FieldName, StorageType, FieldAttributes.Private);

				// Now add a public property to wrap the field, <Name>
				PropertyBuilder Prop = Builder.DefineProperty(Name, System.Reflection.PropertyAttributes.HasDefault, PropertyType, null);

				// Put the property in a category
				ConstructorInfo CategoryCtor = typeof(CategoryAttribute).GetConstructor(new System.Type[] { typeof(string) });
				CustomAttributeBuilder CategoryAttr = new CustomAttributeBuilder(CategoryCtor, new object[] {Category});
				Prop.SetCustomAttribute(CategoryAttr);

				MethodInfo GetValueMethod = null;
				MethodInfo SetValueMethod = null;
				Type GetReturnType = CLRType;
				Type SetArgumentType = CLRType;
				MethodAttributes GetSetAttr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
				BindingFlags MethodSearchFlags = BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public;
				if (Type == ReflectedType.ARRAY)
				{
					GetReturnType = SetArgumentType = CLRType.GetGenericArguments()[0];
					GetValueMethod = StorageType.GetMethod("get_Item", MethodSearchFlags, null, new Type[] { typeof(Int32) }, null);
					SetValueMethod = StorageType.GetMethod("set_Item", MethodSearchFlags, null, new Type[] { typeof(Int32), SetArgumentType }, null);
				}
				else
				{
					GetValueMethod = StorageType.GetMethod("Get", MethodSearchFlags, null, new Type[] { typeof(Int32) }, null);
					SetValueMethod = StorageType.GetMethod("Set", MethodSearchFlags, null, new Type[] { typeof(Int32), CLRType }, null);
				}

				//Build direct access for single values, basically just calls GetValue(0) or SetValue(0, value)
				if (ArrayDim == 1 && Type != ReflectedType.ARRAY)
				{
					// Build get method
					Debug.Assert(GetValueMethod != null);
					MethodBuilder GetMethod = Builder.DefineMethod("get_" + Name, GetSetAttr, GetReturnType, System.Type.EmptyTypes);
					ILGenerator GetOpCodes = GetMethod.GetILGenerator();
					GetOpCodes.Emit(OpCodes.Ldarg_0);               // push this
					GetOpCodes.Emit(OpCodes.Ldfld, Field);          // push field
					GetOpCodes.Emit(OpCodes.Ldc_I4, 0);             // push index of 0
					GetOpCodes.Emit(OpCodes.Call, GetValueMethod);  // call this.GetValue(0)
					GetOpCodes.Emit(OpCodes.Ret);                   // return whatever that left on the stack
					Prop.SetGetMethod(GetMethod);

					// Build set method
					Debug.Assert(SetValueMethod != null);
					MethodBuilder SetMethod = Builder.DefineMethod("set_" + Name, GetSetAttr, null, new Type[] { SetArgumentType });
					ILGenerator SetOpCodes = SetMethod.GetILGenerator();
					SetOpCodes.Emit(OpCodes.Ldarg_0);               // push this
					SetOpCodes.Emit(OpCodes.Ldfld, Field);          // push field
					SetOpCodes.Emit(OpCodes.Ldc_I4, 0);             // push index of 0
					SetOpCodes.Emit(OpCodes.Ldarg_1);               // push value
					SetOpCodes.Emit(OpCodes.Call, SetValueMethod);  // call this.SetValue(0, value)
					SetOpCodes.Emit(OpCodes.Ret);                   // return
					Prop.SetSetMethod(SetMethod);
				}
				else // Build array/indexed accessors
				{
					// Build get method
					Debug.Assert(GetValueMethod != null);
					MethodBuilder GetMethod = Builder.DefineMethod("get_" + Name, GetSetAttr, GetReturnType, new Type[] { typeof(int) });
					ILGenerator GetOpCodes = GetMethod.GetILGenerator();
					GetOpCodes.Emit(OpCodes.Ldarg_0);               // push this
					GetOpCodes.Emit(OpCodes.Ldfld, Field);          // push field
					GetOpCodes.Emit(OpCodes.Ldarg_1);               // push index
					GetOpCodes.Emit(OpCodes.Call, GetValueMethod);  // call this.GetValue(index)
					GetOpCodes.Emit(OpCodes.Ret);                   // return
					Prop.SetGetMethod(GetMethod);

					// Build set method
					Debug.Assert(SetValueMethod != null);
					MethodBuilder SetMethod = Builder.DefineMethod("set_" + Name, GetSetAttr, null, new Type[] { typeof(int), SetArgumentType });
					ILGenerator SetOpCodes = SetMethod.GetILGenerator();
					SetOpCodes.Emit(OpCodes.Ldarg_0);               // push this
					SetOpCodes.Emit(OpCodes.Ldfld, Field);          // push field
					SetOpCodes.Emit(OpCodes.Ldarg_1);               // push index
					SetOpCodes.Emit(OpCodes.Ldarg_2);               // push value
					SetOpCodes.Emit(OpCodes.Call, SetValueMethod);  // call this.SetValue(index, value)
					SetOpCodes.Emit(OpCodes.Ret);                   // return
					Prop.SetSetMethod(SetMethod);
				}
			}
            catch (System.Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
    }

    public void Construct(Object NewObject)
    {
        if (CLRType != null)
        {
            object Instance = null;
            if (Type == ReflectedType.ARRAY)
            {
                ConstructorInfo Ctor = CLRType.GetConstructor(System.Type.EmptyTypes);
                Instance = Ctor.Invoke(null);
            }
            else
            {
                Instance = Array.CreateInstance(CLRType, ArrayDim);
            }

            FieldInfo Field = NewObject.GetType().GetField(FieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Field.SetValue(NewObject, Instance);

            for (int Idx = 0; Idx < ArrayDim; ++Idx)
                SetValue(NewObject, Idx, DefaultValue);
        }
    }

    private object GetFieldValue(object Obj)
    {
        FieldInfo Field = Obj.GetType().GetField(FieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return Field.GetValue(Obj);
    }

    private object Get(object Obj, int Idx)
    {
        Type ClassType = Obj.GetType();
        MethodInfo GetMethod = ClassType.GetMethod("get_" + Name);
		Object ReturnValue = null;
		if (bIsArray)
			ReturnValue = GetMethod.Invoke(Obj, new object[] { Idx });
		else
			ReturnValue = GetMethod.Invoke(Obj, null);
		return ReturnValue;
    }

    private void Set(object Obj, int Idx, object Value)
    {
        object FieldValue = GetFieldValue(Obj);
        int Count = (Type == ReflectedType.ARRAY) ? (FieldValue as IList).Count : (FieldValue as Array).Length;

        if (Idx >= Count)
        {
            if (Type == ReflectedType.ARRAY && Idx == Count)
            {
                (FieldValue as IList).Add(Value);
                return;
            }
            else
            {
                Debug.WriteLine("ReflectedProperty.Set(" + Name + "): Attempted to set value at invalid index " + Idx + ", Count=" + Count);
            }
        }

        Type ClassType = Obj.GetType();
        MethodInfo SetMethod = ClassType.GetMethod("set_" + Name);
		if (bIsArray)
			SetMethod.Invoke(Obj, new object[] { Idx, Value });
		else
			SetMethod.Invoke(Obj, new object[] { Value });
    }

    public virtual void SetValue(object Obj, int Idx, object Value)
    {
        try
        {
            Set(Obj, Idx, Value);
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine(ex.ToString());
        }
    }

    public object GetValue(object Obj, int Idx)
    {
        try
        {
            return Get(Obj, Idx);
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine(ex.ToString());
        }

        return null;
    }

    public void SetValues(object Obj, List<object> Values)
    {
        Debug.Assert(Type == ReflectedType.ARRAY);

        if (Type == ReflectedType.ARRAY && Values is List<object>)
        {
            IList ListInstance = GetFieldValue(Obj) as IList;
            ListInstance.Clear();
            foreach (object Val in Values as List<object>)
            {
                ListInstance.Add(Val);
            }
        }
    }

    public IList GetValues(object Obj)
    {
        Debug.Assert(Type == ReflectedType.ARRAY);

        if (Type == ReflectedType.ARRAY)
        {
            IList ListInstance = GetFieldValue(Obj) as IList;
            return ListInstance;
        }

        return null;
    }
};

class IntProperty : ReflectedProperty
{
	public IntProperty(string InName)
		: base(InName)
	{
		Type = ReflectedType.INT;
	}

	public override Type CLRType
	{
		get { return typeof(int); }
	}

	public override object DefaultValue
	{
		get { return 0; }
	}

	public override void SetValue(object Obj, int Idx, object Value)
	{
		Value = Int32.Parse(Value.ToString());
		base.SetValue(Obj, Idx, Value);
	}
};

class FloatProperty : ReflectedProperty
{
	public FloatProperty(string InName)
		: base(InName)
	{
		Type = ReflectedType.FLOAT;
	}

	public override Type CLRType
	{
		get { return typeof(float); }
	}

	public override object DefaultValue
	{
		get { return 0.0f; }
	}

	public override void SetValue(object Obj, int Idx, object Value)
	{
		Value = float.Parse(Value.ToString());
		base.SetValue(Obj, Idx, Value);
	}
}

class BoolProperty : ReflectedProperty
{
	public BoolProperty(string InName)
		: base(InName)
	{
		Type = ReflectedType.BOOL;
	}

	public override Type CLRType
	{
		get { return typeof(bool); }
	}

	public override object DefaultValue
	{
		get { return false; }
	}

	public override void SetValue(object Obj, int Idx, object Value)
	{
		Value = bool.Parse(Value.ToString());
		base.SetValue(Obj, Idx, Value);
	}
}

class StringProperty : ReflectedProperty
{
	public StringProperty(string InName)
		: base(InName)
	{
		Type = ReflectedType.STRING;
	}

	public override Type CLRType
	{
		get { return typeof(string); }
	}

	public override object DefaultValue
	{
		get { return ""; }
	}

	public override void SetValue(object Obj, int Idx, object Value)
	{
		base.SetValue(Obj, Idx, Value.ToString());
	}
}

class EnumProperty : ReflectedProperty
{
	public string Enum = null;
	public List<string> Values = null;

	public EnumProperty(string InName, string InEnumName, List<string> InEnumValues)
		: base(InName)
	{
		Type = ReflectedType.ENUM;
		Enum = InEnumName;
		Values = InEnumValues;
	}

	public override Type CLRType
	{
		get
		{
			// Check to see if the enum has already been created
			Type CachedType = ReflectedClass.ReflectedModule.GetType(Enum);
			if (CachedType != null)
				return CachedType;

			EnumBuilder EnumBuilderInstance = ReflectedClass.ReflectedModule.DefineEnum(Enum, TypeAttributes.Public, typeof(Int32));
			int Idx = 0;
			foreach (string Value in Values)
			{
				EnumBuilderInstance.DefineLiteral(Value, Idx++);
			}

			// This will also cache the enum type into the ReflectedModule
			Type EnumType = EnumBuilderInstance.CreateType();
			return EnumType;
		}
	}

	public override object DefaultValue
	{
		get { return System.Enum.GetValues(CLRType).GetValue(0); }
	}

	public override void SetValue(object Obj, int Idx, object Value)
	{
		Value = System.Enum.Parse(CLRType, Value.ToString());
		base.SetValue(Obj, Idx, Value);
	}
}

class ArrayProperty : ReflectedProperty
{
	public ReflectedProperty Inner = null;

	public ArrayProperty(string InName, ReflectedProperty InnerProperty)
		: base(InName)
	{
		Type = ReflectedType.ARRAY;
		Inner = InnerProperty;
	}

	public override Type CLRType
	{
		get
		{
			Debug.Assert(Inner != null && Inner.CLRType != null);
			Type ListType = typeof(List<>).MakeGenericType(Inner.CLRType);
			return ListType;
		}
	}

	public override object DefaultValue
	{
		get { return Inner.DefaultValue; }
	}
}

class ObjectProperty : ReflectedProperty
{
	public ReflectedClass Class = null;

	public ObjectProperty(string InName, ReflectedClass InClass)
		: base(InName)
	{
		Type = ReflectedType.CLASS;
		Class = InClass;
	}

	public override Type CLRType
	{
		get
		{
			Debug.Assert(Class != null);
			return Class.CLRType;
		}
	}

	public override object DefaultValue
	{
		get { return Class.Construct(); }
	}
}
