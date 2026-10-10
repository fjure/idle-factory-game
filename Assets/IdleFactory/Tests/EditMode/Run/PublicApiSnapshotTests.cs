using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using IdleFactory.Run.Application;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// Reflection baseline of the public surface of the engine-free <c>IdleFactory.Core</c> assembly (QA-16).
    /// Every externally visible type and every public or protected member is rendered as one sorted,
    /// culture-invariant line and compared with the committed
    /// <c>Fixtures/PublicApi/core-public-api.txt</c>. Any difference fails the test, so a refactor that is meant
    /// to be behaviour-preserving (for example the RunPersistence split) cannot change the surface unnoticed.
    ///
    /// To change the surface deliberately: set the environment variable
    /// <c>IDLEFACTORY_REGENERATE_PUBLIC_API=1</c>, run this test once (it rewrites the baseline and reports
    /// Inconclusive), unset the variable, review the diff of the baseline file and commit it together with the
    /// change that caused it. Reflection is used here only; product code never reflects.
    /// </summary>
    public sealed class PublicApiSnapshotTests
    {
        private const string BaselineRelativeToAssets = "IdleFactory/Tests/EditMode/Fixtures/PublicApi/core-public-api.txt";
        private const string RegenerateVariable = "IDLEFACTORY_REGENERATE_PUBLIC_API";

        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        [Test]
        public void CorePublicSurfaceMatchesCommittedBaseline()
        {
            string actual = Render(typeof(RunPersistence).Assembly);
            string path = Path.Combine(UnityEngine.Application.dataPath, BaselineRelativeToAssets);

            if (Environment.GetEnvironmentVariable(RegenerateVariable) == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, actual, new UTF8Encoding(false));
                Assert.Inconclusive("Baseline regenerated at " + BaselineRelativeToAssets + "; unset " +
                    RegenerateVariable + " and review the diff.");
            }

            Assert.That(File.Exists(path), Is.True, "Missing baseline " + BaselineRelativeToAssets);
            string expected = File.ReadAllText(path).Replace("\r\n", "\n");
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                Assert.Fail("IdleFactory.Core public surface differs from the baseline. " + Diff(expected, actual) +
                    " If the change is deliberate, regenerate with " + RegenerateVariable + "=1.");
            }
        }

        [Test]
        public void RenderingIsDeterministic()
        {
            Assembly core = typeof(RunPersistence).Assembly;
            Assert.That(Render(core), Is.EqualTo(Render(core)));
        }

        private static string Diff(string expected, string actual)
        {
            var e = new HashSet<string>(expected.Split('\n'), StringComparer.Ordinal);
            var a = new HashSet<string>(actual.Split('\n'), StringComparer.Ordinal);
            var sb = new StringBuilder();
            int shown = 0;
            foreach (string line in expected.Split('\n'))
            {
                if (!a.Contains(line) && shown++ < 20) sb.Append("\n- ").Append(line);
            }
            foreach (string line in actual.Split('\n'))
            {
                if (!e.Contains(line) && shown++ < 40) sb.Append("\n+ ").Append(line);
            }
            return sb.ToString();
        }

        private static string Render(Assembly assembly)
        {
            var lines = new List<string>();
            foreach (Type type in assembly.GetTypes().Where(t => t.IsVisible))
            {
                string typeName = TypeName(type);
                lines.Add(TypeHeader(type, typeName));
                foreach (string member in Members(type)) lines.Add("  " + typeName + " :: " + member);
            }

            lines.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder();
            foreach (string line in lines) sb.Append(line).Append('\n');
            return sb.ToString();
        }

        private static string TypeHeader(Type type, string name)
        {
            string kind = type.IsEnum ? "enum" : type.IsInterface ? "interface" : type.IsValueType ? "struct" :
                typeof(Delegate).IsAssignableFrom(type) ? "delegate" : "class";
            string modifiers = "";
            if (type.IsClass && !typeof(Delegate).IsAssignableFrom(type))
            {
                modifiers = type.IsAbstract && type.IsSealed ? "static " : type.IsAbstract ? "abstract " :
                    type.IsSealed ? "sealed " : "";
            }
            else if (type.IsValueType && !type.IsEnum && type.GetCustomAttributes(false)
                .Any(a => a.GetType().Name == "IsReadOnlyAttribute"))
            {
                modifiers = "readonly ";
            }

            var bases = new List<string>();
            if (type.BaseType != null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType) &&
                type.BaseType != typeof(Enum))
            {
                bases.Add(TypeName(type.BaseType));
            }
            bases.AddRange(type.GetInterfaces().Select(TypeName).OrderBy(n => n, StringComparer.Ordinal));
            if (type.IsEnum) bases.Add(TypeName(Enum.GetUnderlyingType(type)));

            string header = "type " + modifiers + kind + " " + name;
            if (bases.Count > 0) header += " : " + string.Join(", ", bases);
            return header;
        }

        private static IEnumerable<string> Members(Type type)
        {
            foreach (ConstructorInfo c in type.GetConstructors(Declared).Where(c => IsExposed(c.IsPublic, c.IsFamily, c.IsFamilyOrAssembly)))
            {
                yield return "ctor " + Visibility(c.IsPublic) + (c.IsStatic ? "static " : "") + "(" + Params(c.GetParameters()) + ")";
            }

            foreach (FieldInfo f in type.GetFields(Declared).Where(f => IsExposed(f.IsPublic, f.IsFamily, f.IsFamilyOrAssembly)))
            {
                string mods = f.IsLiteral ? "const " : (f.IsStatic ? "static " : "") + (f.IsInitOnly ? "readonly " : "");
                string value = f.IsLiteral ? " = " + ConstantText(f.GetRawConstantValue()) : "";
                yield return "field " + Visibility(f.IsPublic) + mods + TypeName(f.FieldType) + " " + f.Name + value;
            }

            foreach (PropertyInfo p in type.GetProperties(Declared))
            {
                MethodInfo get = Exposed(p.GetMethod);
                MethodInfo set = Exposed(p.SetMethod);
                if (get == null && set == null) continue;
                MethodInfo any = get ?? set;
                var accessors = new List<string>();
                if (get != null) accessors.Add("get" + (get.IsPublic == any.IsPublic ? "" : "/" + Visibility(get.IsPublic).Trim()));
                if (set != null) accessors.Add("set" + (set.IsPublic == any.IsPublic ? "" : "/" + Visibility(set.IsPublic).Trim()));
                ParameterInfo[] index = p.GetIndexParameters();
                string indexer = index.Length > 0 ? "[" + Params(index) + "]" : "";
                yield return "property " + Visibility(any.IsPublic) + Method(any) + TypeName(p.PropertyType) + " " + p.Name +
                    indexer + " { " + string.Join("; ", accessors) + " }";
            }

            foreach (EventInfo e in type.GetEvents(Declared))
            {
                MethodInfo add = Exposed(e.AddMethod);
                if (add == null) continue;
                yield return "event " + Visibility(add.IsPublic) + Method(add) + TypeName(e.EventHandlerType) + " " + e.Name;
            }

            foreach (MethodInfo m in type.GetMethods(Declared))
            {
                if (m.IsSpecialName || Exposed(m) == null) continue;
                string generics = m.IsGenericMethodDefinition
                    ? "<" + string.Join(", ", m.GetGenericArguments().Select(g => g.Name)) + ">" : "";
                yield return "method " + Visibility(m.IsPublic) + Method(m) + TypeName(m.ReturnType) + " " + m.Name + generics +
                    "(" + Params(m.GetParameters()) + ")";
            }
        }

        private static MethodInfo Exposed(MethodInfo m) =>
            m != null && IsExposed(m.IsPublic, m.IsFamily, m.IsFamilyOrAssembly) ? m : null;

        private static bool IsExposed(bool isPublic, bool isFamily, bool isFamilyOrAssembly) =>
            isPublic || isFamily || isFamilyOrAssembly;

        private static string Visibility(bool isPublic) => isPublic ? "public " : "protected ";

        private static string Method(MethodInfo m)
        {
            string s = m.IsStatic ? "static " : "";
            if (m.IsAbstract) s += "abstract ";
            else if (m.IsVirtual && m.IsFinal) s += "sealed override ";
            else if (m.IsVirtual && m.GetBaseDefinition() != m) s += "override ";
            else if (m.IsVirtual && !m.DeclaringType.IsInterface) s += "virtual ";
            return s;
        }

        private static string Params(ParameterInfo[] parameters) =>
            string.Join(", ", parameters.Select(p =>
            {
                Type t = p.ParameterType;
                string prefix = "";
                if (t.IsByRef)
                {
                    prefix = p.IsOut ? "out " : p.IsIn ? "in " : "ref ";
                    t = t.GetElementType();
                }
                else if (p.GetCustomAttributes(false).Any(a => a.GetType().Name == "ParamArrayAttribute"))
                {
                    prefix = "params ";
                }
                string suffix = p.HasDefaultValue ? " = " + ConstantText(p.RawDefaultValue) : "";
                return prefix + TypeName(t) + " " + p.Name + suffix;
            }));

        private static string ConstantText(object value)
        {
            if (value == null) return "null";
            if (value is string s) return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            if (value is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);
            return value.ToString();
        }

        private static string TypeName(Type type)
        {
            if (type.IsByRef) return TypeName(type.GetElementType()) + "&";
            if (type.IsArray) return TypeName(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            if (type.IsGenericParameter) return type.Name;
            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                string name = definition.FullName ?? definition.Name;
                int tick = name.IndexOf('`');
                if (tick >= 0) name = name.Substring(0, tick);
                return name.Replace('+', '.') + "<" + string.Join(", ", type.GetGenericArguments().Select(TypeName)) + ">";
            }
            return (type.FullName ?? type.Name).Replace('+', '.');
        }
    }
}
