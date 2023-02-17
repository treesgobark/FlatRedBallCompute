#if WINDOWS_8 || IOS || ANDROID
#define NO_CODE_EMIT
#endif

using System;
using System.Collections.Generic;
using System.Reflection;
#if !NO_CODE_EMIT
using System.Reflection.Emit;
#endif

#if WpfDataUi
namespace WpfDataUi

#else
namespace FlatRedBall.Instructions.Reflection
#endif
{
    public abstract class LateBinder
    {
        static Dictionary<Type, LateBinder> mLateBinders = new Dictionary<Type, LateBinder>();

        public static LateBinder GetInstance(Type type)
        {
            if (!mLateBinders.ContainsKey(type))
            {
                Type t = typeof(LateBinder<>).MakeGenericType(
                    type);
                object obj = Activator.CreateInstance(t);

                mLateBinders.Add(type, obj as LateBinder);
            }
            return mLateBinders[type];

        }

        public static object GetValueStatic(object target, string name)
        /// <summary>
        /// Returns the value of the variable obtained by name. This named "Static"
        /// because it is a static version of the GetValue variable on LateBinderinstances.
        /// This can be used to obtain values without needing to first create an instance of a late binder.
        /// </summary>
        /// <param name="target">The object from which to get a value.</param>
        /// <param name="name">The variable name.</param>
        /// <returns></returns>
        {
            return GetInstance(target.GetType()).GetValue(target, name);
        }

        public static bool SetValueStatic(object target, string name, object value)
        {
            return GetInstance(target.GetType()).SetValue(target, name, value);
        }

        public static bool TryGetValueStatic(object target, string name, out object result)
        {
            return GetInstance(target.GetType()).TryGetValue(target, name, out result);
        }

        public abstract object GetValue(object target, string name);
        public abstract bool IsReadOnly(string name);
        public abstract bool IsWriteOnly(string name);
        public abstract bool TryGetValue(object target, string name, out object result);
        public abstract bool SetValue(object target, string name, object value);
    }



    /// <summary>
    /// Provides a simple interface to late bind a class.
    /// </summary>
    /// <remarks>The first time you attempt to get or set a property, it will dynamically generate the get and/or set 
    /// methods and cache them internally.  Subsequent gets uses the dynamic methods without having to query the type's 
    /// meta data.</remarks>
    public sealed class LateBinder<T> : LateBinder
    {
        internal class SetPropertyInfo
        {
            public SetHandler Delegate;
            public PropertyInfo PropertyInfo;

            internal void Invoke(object source, object value)
            {
#if DEBUG
                if(value != null)
                {
                    var valueType = value.GetType();
                    var propertyType = PropertyInfo.PropertyType; 
                    var isAssignable = propertyType.IsAssignableFrom(valueType);
                    if(!isAssignable)
                    {
                        var message =
                            $"Attempting to set property {PropertyInfo.Name} of type {propertyType} to value {value} with type {value.GetType()}";
                        throw new InvalidOperationException(message);
                    }
                }
#endif
                Delegate(source, value);
            }
        }
        #region Fields

        HashSet<string> mFieldsSet = new HashSet<string>();
        HashSet<string> mPropertieSet = new HashSet<string>();

        private Type mType;
        private Dictionary<string, GetHandler> mPropertyGet;
        private Dictionary<string, SetPropertyInfo> mPropertySet;

        private Dictionary<Type, List<string>> mFields;

        private T mTarget = default(T);

        private static LateBinder<T> _instance;

        #endregion

        #region Properties

        public static LateBinder<T> Instance
        {
            get { return _instance; }
        }


        /// <summary>
        /// The instance that this binder operates on by default
        /// </summary>
        /// <remarks>This can be overridden by the caller explicitly passing a target to the indexer</remarks>
        public T Target
        {
            get { return mTarget; }
            set { mTarget = value; }
        }

        /// <summary>
        /// Gets or Sets the supplied property on the contained <seealso cref="Instance"/>
        /// </summary>
        /// <exception cref="InvalidOperationException">Throws if the contained Instance is null.</exception>
        public object this[string propertyName]
        {
            get
            {
                ValidateInstance();
                return this[mTarget, propertyName];
            }
            set
            {
                ValidateInstance();
                this[mTarget, propertyName] = value;
            }
        }

        /// <summary>
        /// Gets or Sets the supplied property on the supplied target
        /// </summary>
        public object this[T target, string propertyName]
        {
            get
            {
                ValidateGetter(ref propertyName);
                return mPropertyGet[propertyName](target);
            }
            set
            {
                ValidateSetter(ref propertyName);
                mPropertySet[propertyName].Invoke(target, value);
            }
        }


        #endregion

        #region Methods

        #region Constructors

        static LateBinder()
        {
            _instance = new LateBinder<T>();
        }


        public LateBinder(T instance)
            : this()
        {
            mTarget = instance;
        }

        public LateBinder()
        {
            mType = typeof(T);
            mPropertyGet = new Dictionary<string, GetHandler>();
            mPropertySet = new Dictionary<string, SetPropertyInfo>();

            mFields = new Dictionary<Type, List<string>>();
        }
        #endregion

        #endregion

        #region Public Methods

        public override object GetValue(object target, string name)
        {
            if (mFieldsSet.Contains(name))
            {
                return GetField(target, name);
            }
            else if (mPropertieSet.Contains(name))
            {
                return GetProperty(target, name);
            }
            else
            {
#if UWP
                if (mType.GetField(name) != null)
#else
                GetFieldRecursive(mType, name, out FieldInfo fieldInfo, out Type throwaway);
                if (fieldInfo != null)
#endif
                {
                    mFieldsSet.Add(name);
                    return GetField(target, name);
                }
                else
                {
                    mPropertieSet.Add(name);
                    return GetProperty(target, name);
                }
            }
        }

        private void GetFieldRecursive(Type type, string fieldName, out FieldInfo field, out Type ownerType)
        {
#if UWP
            field = type.GetField(fieldName);
#else
            field = type.GetField(fieldName, mGetFieldBindingFlags);
#endif
            ownerType = null;

            if(field != null)
            {
                ownerType = type;
            }
#if UWP
            else if (field == null && type.GetTypeInfo().BaseType != null)
            {
                GetFieldRecursive(type.GetTypeInfo().BaseType, fieldName, out field, out ownerType);
            }
#else
            else if(field == null && type.BaseType != null)
            {
                GetFieldRecursive(type.BaseType, fieldName, out field, out ownerType);
            }
#endif
        }

        public override bool IsReadOnly(string name)
        {
            FieldInfo fieldInfo;
            PropertyInfo propertyInfo;

            fieldInfo = mType.GetField(name);

            if (fieldInfo != null)
            {
                return false; // need to adjust this eventually...assuming false now
            }
            else
            {
                propertyInfo = mType.GetProperty(name);
                if (propertyInfo != null)
                {
                    return propertyInfo.CanWrite == false;
                }
            }

            return false;
        }

        public override bool IsWriteOnly(string name)
        {
            FieldInfo fieldInfo;
            PropertyInfo propertyInfo;

            fieldInfo = mType.GetField(name);

            if (fieldInfo != null)
            {
                return false; // need to adjust this eventually...assuming false now
            }
            else
            {
                propertyInfo = mType.GetProperty(name);
                if (propertyInfo != null)
                {
                    return propertyInfo.CanRead == false;
                }
            }

            return false;

        }

        public override bool TryGetValue(object target, string name, out object result)
        {
            if (mFieldsSet.Contains(name))
            {
                result = GetField(target, name);
                return true;
            }
            else if (mPropertieSet.Contains(name))
            {
                result = GetProperty(target, name);
                return true;
            }
            else
            {
#if WINDOWS_8 || UWP
                if (mType.GetField(name) != null)
#else
                if (mType.GetField(name, mGetFieldBindingFlags) != null)
#endif
                {
                    mFieldsSet.Add(name);
                    result = GetField(target, name);
                    return true;
                }
                else if (mType.GetProperty(name) != null)
                {
                    mPropertieSet.Add(name);
                    result = GetProperty(target, name);
                    return true;
                }
                else
                {
                    result = null;
                    return false;
                }

            }
        }

        public override bool SetValue(object target, string name, object value)
        {
            var wasSet = false;
            if (mFieldsSet.Contains(name))
            {
                // do nothing currently
                SetField(target, name, value);
                wasSet = true;
            }
            else if (mPropertieSet.Contains(name))
            {
                SetProperty(target, name, value);
                wasSet = true;
            }
            else
            {
                try
                {
    #if WINDOWS_8 || UWP
                    if (mType.GetField(name) != null)
    #else
                    if (mType.GetField(name, mGetFieldBindingFlags) != null)
    #endif
                    {
                        mFieldsSet.Add(name);

                        SetField(target, name, value);
                        wasSet = true;
                    }
                    else
                    {
                        mPropertieSet.Add(name);
                        SetProperty(target, name, value);
                        wasSet = true;
                    }
                }
                catch(InvalidCastException innerException)
                {
                    throw new InvalidOperationException(
                        $"Error setting {name} to {value} (value type {value?.GetType()})", innerException);
                }
            }

            return wasSet;
        }

        private void SetField(object target, string name, object value)
        {



#if NO_CODE_EMIT
            
            // If a field is 
            // set to a struct
            // like Vector3, then
            // the value that is set
            // will be boxed if using
            // SetValue.  On the PC we
            // use SetValueDirect, but that
            // isn't available on the CompactFramework.
            // I've asked a question about it here:
            // http://stackoverflow.com/questions/11698172/how-to-set-a-field-of-a-struct-type-on-an-object-in-windows-phone-7
            FieldInfo fieldInfo = target.GetType().GetField(
                name);

            fieldInfo.SetValue(target, value);

#else
            FieldInfo fieldInfo = target.GetType().GetField(
                name);


#if DEBUG
            try
            {
#endif



                fieldInfo.SetValueDirect(
                    __makeref(target), value);


#if DEBUG
            }
            catch (Exception e)
            {
                if(fieldInfo == null)
                {
                    throw new Exception("Could nto find field by the name " + name );
                }
                else
                {

                    throw new Exception("Error trying to set field " + name + " which is of type " + fieldInfo.FieldType + ".\nTrying to set to " + value + " of type " + value.GetType());
                }
            }
#endif


#endif
        }


        /// <summary>
        /// Sets the supplied property on the supplied target
        /// </summary>
        /// <typeparam name="K">the type of the value</typeparam>
        public void SetProperty<K>(object target, string propertyName, K value)
        {
#if NO_CODE_EMIT

            // find out if this is a property or field
            Type type = typeof(T);

            PropertyInfo propertyInfo = type.GetProperty(propertyName);

            if (propertyInfo != null)
            {
                propertyInfo.SetValue(target, value, null);
            }

            else
            {
                FieldInfo fieldInfo = type.GetField(propertyName);

                if (fieldInfo != null)
                {
                    fieldInfo.SetValue(target, value);
                }
                else
                {
                    throw new ArgumentException("Cannot find property or field with the name " + propertyName);
                }

            }


#else
            ValidateSetter(ref propertyName);

            if (mPropertySet.ContainsKey(propertyName))
            {
                try
                {
                    mPropertySet[propertyName].Invoke(target, value);
                }
                catch (System.AccessViolationException ex)
                {
                    throw new Exception($"Could not set {propertyName} to {value} with type {value?.GetType()}, probably because the types do not match", ex);
                }
            }
            else
            {
                // This is probably not a property so see if it is a field.

                FieldInfo fieldInfo = mType.GetField(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

                if (fieldInfo == null)
                {
                    string errorMessage =
                        $"LateBinder could not find a field or property by the name of {propertyName}" +
                        $" in the class {mType}.  Check the name of the property to verify if it is correct.";
                    throw new System.MemberAccessException(errorMessage);
                }
                else
                {

#if WINDOWS_8
                    fieldInfo.SetValue(target, value);
#else
                    // I don't know why we branch here....Can we not call SetValue on public values?
                    if (!fieldInfo.IsPublic)
                    {
                        fieldInfo.SetValue(target, value);
                    }
                    else
                    {
                        object[] args = { value };
                        mType.InvokeMember(propertyName, BindingFlags.SetField, null, target, args);
                    }
#endif

                }
            }
#endif
        }

#if !UWP
        static BindingFlags mGetFieldBindingFlags = BindingFlags.GetField | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | 
            BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
#endif

        public object GetField(object target, string fieldName)
        {


            if (target == null)
            {
#if UWP
                FieldInfo fieldInfo = mType.GetField(fieldName);
#else
                FieldInfo fieldInfo = mType.GetField(fieldName, mGetFieldBindingFlags);
#endif
                return fieldInfo.GetValue(null);

            }
            else
            {
#if UWP
                return mType.GetField(fieldName).GetValue(target);
#else

                Binder binder = null;
                object[] args = null;

                // use this to get the recurisve type in case it's defined by base
                GetFieldRecursive(mType, fieldName, out FieldInfo fieldInfo, out Type fieldOwner);

                return fieldOwner.InvokeMember(
                   fieldName,
                   mGetFieldBindingFlags,
                   binder,
                   target,
                   args
                   );
#endif
            }
        }

        public ReturnType GetField<ReturnType>(T target, string propertyName)
        {
            return (ReturnType)GetField(target, propertyName);
        }

        /// <summary>
        /// Gets  the supplied property on the supplied target
        /// </summary>
        /// <typeparam name="K">The type of the property being returned</typeparam>
        public K GetProperty<K>(T target, string propertyName)
        {
            return (K)GetProperty(target, propertyName);
        }

        public object GetProperty(object target, string propertyName)
        {
#if NO_CODE_EMIT
            // SLOW, but still works
            return GetPropertyThroughReflection(target, propertyName);
#else
            // June 11, 2011
            // Turns out that
            // getters for value
            // types don't work properly.
            // I found this out by trying to
            // get the X value on a System.Drawing.Rectangle
            // which was 0, but it kept returning a value of
            // 2 billion.  Checking for value types and using 
            // regular reflection fixes this problem.
            if (target == null || typeof(T).IsValueType)
            {
                // SLOW, but still works
                return GetPropertyThroughReflection(target, propertyName);
            }
            else
            {
                ValidateGetter(ref propertyName);

                GetHandler getHandler = mPropertyGet[propertyName];

                // getter may throw an exception.  We don't want the grid to blow up in that case, so we'll return null:
                try
                {
                    return getHandler(target);
                }
                catch
                {
                    return null;
                }
            }
#endif


        }

        private static object GetPropertyThroughReflection(object target, string propertyName)
        {
#if UWP
            PropertyInfo pi = typeof(T).GetProperty(propertyName);
#else
            PropertyInfo pi = typeof(T).GetProperty(propertyName, mGetterBindingFlags);
#endif

            if (pi == null)
            {
                string message = "Could not find the property " + propertyName + "\n\nAvailableProperties:\n\n";
#if UWP
                IEnumerable<PropertyInfo> properties = typeof(T).GetProperties();
#else
                PropertyInfo[] properties = typeof(T).GetProperties(mGetterBindingFlags);
#endif

                foreach (PropertyInfo containedProperty in properties)
                {
                    message += containedProperty.Name + "\n";

                }

                throw new InvalidOperationException(message);
            }

            return pi.GetValue(target, null);
        }

#endregion

#region Private Helpers
        private void ValidateInstance()
        {
            if (mTarget == null)
            {
                throw new InvalidOperationException("Instance property must not be null");
            }
        }
        private void ValidateSetter(ref string propertyName)
        {
            if (!mPropertySet.ContainsKey(propertyName))
            {
#if WINDOWS_8 || UWP
                PropertyInfo propertyInfo = mType.GetProperty(propertyName);
#else
                BindingFlags bindingFlags =
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.Static;
                PropertyInfo propertyInfo = mType.GetProperty(propertyName, bindingFlags);
#endif
                if (propertyInfo != null && propertyInfo.CanWrite)
                {
                    var setInfo = new SetPropertyInfo();
                    setInfo.Delegate = DynamicMethodCompiler.CreateSetHandler(mType, propertyInfo);
                    setInfo.PropertyInfo = propertyInfo;
                    mPropertySet.Add(propertyName, setInfo);
                }

            }
        }

#if !WINDOWS_8 && !UWP
        static BindingFlags mGetterBindingFlags = 
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.FlattenHierarchy;                
#endif

        private void ValidateGetter(ref string propertyName)
        {
            if (!mPropertyGet.ContainsKey(propertyName))
            {
                Type type = mType;
#if WINDOWS_8 || UWP
                PropertyInfo propertyInfo = mType.GetProperty(propertyName);
#else
                PropertyInfo propertyInfo = mType.GetProperty(propertyName, mGetterBindingFlags);

                var baseType = mType.BaseType;
                while(baseType != null && propertyInfo == null)
                {
                    propertyInfo = baseType.GetProperty(propertyName, mGetterBindingFlags);
                    if(propertyInfo != null)
                    {
                        type = baseType;
                    }
                    else
                    {
                        baseType = baseType.BaseType;
                    }
                }
#endif
                if (propertyInfo != null)
                {

                    mPropertyGet[propertyName] = DynamicMethodCompiler.CreateGetHandler(type, propertyInfo);
                }
            }
        }
#endregion

#region Contained Classes
        internal delegate object GetHandler(object source);
        internal delegate void SetHandler(object source, object value);
        internal delegate object InstantiateObjectHandler();

        /// <summary>
        /// provides helper functions for late binding a class
        /// </summary>
        /// <remarks>
        /// Class found here:
        /// http://www.codeproject.com/useritems/Dynamic_Code_Generation.asp
        /// </remarks>
        internal sealed class DynamicMethodCompiler
        {
            // DynamicMethodCompiler
            private DynamicMethodCompiler() { }

            // CreateInstantiateObjectDelegate
            internal static InstantiateObjectHandler CreateInstantiateObjectHandler(Type type)
            {
#if !NO_CODE_EMIT
                ConstructorInfo constructorInfo = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[0], null);
                if (constructorInfo == null)
                {
                    throw new ApplicationException(string.Format("The type {0} must declare an empty constructor (the constructor may be private, internal, protected, protected internal, or public).", type));
                }

                DynamicMethod dynamicMethod = new DynamicMethod("InstantiateObject", MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard, typeof(object), null, type, true);
                ILGenerator generator = dynamicMethod.GetILGenerator();
                generator.Emit(OpCodes.Newobj, constructorInfo);
                generator.Emit(OpCodes.Ret);
                return (InstantiateObjectHandler)dynamicMethod.CreateDelegate(typeof(InstantiateObjectHandler));
#else
                throw new NotSupportedException();
#endif
            }

            // CreateGetDelegate
            internal static GetHandler CreateGetHandler(Type type, PropertyInfo propertyInfo)
            {
#if !NO_CODE_EMIT
                MethodInfo getMethodInfo = propertyInfo.GetGetMethod(true);
                DynamicMethod dynamicGet = CreateGetDynamicMethod(type);
                ILGenerator getGenerator = dynamicGet.GetILGenerator();

                getGenerator.Emit(OpCodes.Ldarg_0);
                getGenerator.Emit(OpCodes.Call, getMethodInfo);
                BoxIfNeeded(getMethodInfo.ReturnType, getGenerator);
                getGenerator.Emit(OpCodes.Ret);

                var getHandler = (GetHandler)dynamicGet.CreateDelegate(typeof(GetHandler));
                return getHandler;
#else
                throw new NotSupportedException();
#endif
            }

            // CreateGetDelegate
            internal static GetHandler CreateGetHandler(Type type, FieldInfo fieldInfo)
            {
#if !NO_CODE_EMIT
                DynamicMethod dynamicGet = CreateGetDynamicMethod(type);
                ILGenerator getGenerator = dynamicGet.GetILGenerator();

                getGenerator.Emit(OpCodes.Ldarg_0);
                getGenerator.Emit(OpCodes.Ldfld, fieldInfo);
                BoxIfNeeded(fieldInfo.FieldType, getGenerator);
                getGenerator.Emit(OpCodes.Ret);

                return (GetHandler)dynamicGet.CreateDelegate(typeof(GetHandler));
#else
                throw new NotSupportedException();
#endif
            }

            // CreateSetDelegate
            internal static SetHandler CreateSetHandler(Type type, PropertyInfo propertyInfo)
            {
#if !NO_CODE_EMIT
                MethodInfo setMethodInfo = propertyInfo.GetSetMethod(true);

                DynamicMethod dynamicSet = CreateSetDynamicMethod(type);
                ILGenerator setGenerator = dynamicSet.GetILGenerator();

                setGenerator.Emit(OpCodes.Ldarg_0);
                setGenerator.Emit(OpCodes.Ldarg_1);
                UnboxIfNeeded(setMethodInfo.GetParameters()[0].ParameterType, setGenerator);
                setGenerator.Emit(OpCodes.Call, setMethodInfo);
                setGenerator.Emit(OpCodes.Ret);

                return (SetHandler)dynamicSet.CreateDelegate(typeof(SetHandler));
#else
                throw new NotSupportedException();
#endif
            }

            // CreateSetDelegate
            internal static SetHandler CreateSetHandler(Type type, FieldInfo fieldInfo)
            {
#if !NO_CODE_EMIT
                DynamicMethod dynamicSet = CreateSetDynamicMethod(type);
                ILGenerator setGenerator = dynamicSet.GetILGenerator();

                setGenerator.Emit(OpCodes.Ldarg_0);
                setGenerator.Emit(OpCodes.Ldarg_1);
                UnboxIfNeeded(fieldInfo.FieldType, setGenerator);
                setGenerator.Emit(OpCodes.Stfld, fieldInfo);
                setGenerator.Emit(OpCodes.Ret);

                return (SetHandler)dynamicSet.CreateDelegate(typeof(SetHandler));
#else
                throw new NotSupportedException();
#endif
            }

#if !NO_CODE_EMIT
            // CreateGetDynamicMethod
            private static DynamicMethod CreateGetDynamicMethod(Type type)
            {
                return new DynamicMethod("DynamicGet", typeof(object), new Type[] { typeof(object) }, type, true);
            }

            // CreateSetDynamicMethod
            private static DynamicMethod CreateSetDynamicMethod(Type type)
            {
                return new DynamicMethod("DynamicSet", typeof(void), new Type[] { typeof(object), typeof(object) }, type, true);
            }

            // BoxIfNeeded
            private static void BoxIfNeeded(Type type, ILGenerator generator)
            {
                if (type.IsValueType)
                {
                    generator.Emit(OpCodes.Box, type);
                }
            }

            // UnboxIfNeeded
            private static void UnboxIfNeeded(Type type, ILGenerator generator)
            {
                if (type.IsValueType)
                {
                    generator.Emit(OpCodes.Unbox_Any, type);
                }
            }
#endif
        }
#endregion
    }
}
