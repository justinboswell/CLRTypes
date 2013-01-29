
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

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics;
using System;

public class ReflectedClass
{
    public string Name;
    public Dictionary<string, ReflectedProperty> Properties;
    ConstructorInfo Ctor = null;

    public System.Type CLRType
    {
        get
        {
            return ReflectedClass.ReflectedModule.GetType(Name);
        }
    }

    private ReflectedClass(string InName, Dictionary<string, ReflectedProperty> InProperties)
    {
        Name = InName;
        Properties = InProperties;

        InitCLRType();
    }

    public static ReflectedClass Create(string InName, List<ReflectedProperty> InProperties)
    {
        Dictionary<string, ReflectedProperty> PropertyMap = new Dictionary<string, ReflectedProperty>();
        foreach (ReflectedProperty Property in InProperties)
            PropertyMap.Add(Property.Name, Property);

        ReflectedClass Class = new ReflectedClass(InName, PropertyMap);
        return Class;
    }

    void InitCLRType()
    {
        // See if the type already exists in the assembly, if not, create it
        if (CLRType == null)
        {
            try
            {
                TypeBuilder Builder = ReflectedClass.ReflectedModule.DefineType(Name, TypeAttributes.Public);
                Builder.DefineDefaultConstructor(MethodAttributes.Public);

                foreach (ReflectedProperty Property in Properties.Values)
                {
                    Property.Emit(Builder);
                }

                Builder.CreateType();
                Ctor = Builder.GetConstructor(System.Type.EmptyTypes);
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
    }

    public Object Construct()
    {
        Debug.Assert(Ctor != null);

        Object NewObject = Ctor.Invoke(null);
        foreach (ReflectedProperty Property in Properties.Values)
        {
            Property.Construct(NewObject);
        }

        return NewObject;
    }

    // Type creation stuff
    public static ModuleBuilder ReflectedModule = null;
	private static AssemblyBuilder AsmBuilder = null;
    public static void CreateReflectedAssembly()
    {
        AssemblyName AsmName = new AssemblyName("ReflectedTypes");
        AsmBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(AsmName, AssemblyBuilderAccess.RunAndSave);
        ReflectedModule = AsmBuilder.DefineDynamicModule(AsmName.Name, AsmName.Name + ".dll");
    }

	public static void SaveReflectedAssembly()
	{
		AsmBuilder.Save("ReflectedTypes.dll");
	}
};
