#region BSD license
/*
Copyright © 2015, KimikoMuffin.
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer. 
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.
3. The names of its contributors may not be used to endorse or promote 
   products derived from this software without specific prior written 
   permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
#endregion

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DieFledermaus.Cli.Globalization;

namespace DieFledermaus.Cli
{
    internal class ClParser : IDisposable
    {
        private List<ClParam> _params = new List<ClParam>();
        public List<ClParam> Params { get { return _params; } }

        private ClParamValueBase _rawParam;
        public ClParamValueBase RawParam
        {
            get { return _rawParam; }
            set
            {
                if (_disposed)
                    throw new ObjectDisposedException(null);
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                _rawParam = value;
            }
        }

        private HashSet<ClParam> _setParams = new HashSet<ClParam>(), _badParams = new HashSet<ClParam>();

        public bool Parse(string[] args)
        {
            if (_disposed)
                throw new ObjectDisposedException(null);

            Dictionary<string, ClParam> stringDict = new Dictionary<string, ClParam>(_params.Count, StringComparer.OrdinalIgnoreCase);
            Dictionary<char, ClParam> charDict = new Dictionary<char, ClParam>(_params.Count);
            List<ClParam> orderedParams = new List<ClParam>();
            bool result = false;

            for (int i = 0; i < _params.Count; i++)
            {
                ClParam curParam = _params[i];

                curParam.SetChanged += CurParam_SetChanged;

                if (curParam.LongNames.Length != 0)
                {
                    for (int j = 0; j < curParam.LongNames.Length; j++)
                        stringDict.Add("--" + curParam.LongNames[j], curParam);
                }

                if (curParam.ShortName != '\0')
                    charDict.Add(curParam.ShortName, curParam);
            }
            int end = args.Length - 1;
            for (int i = 0; i < args.Length; i++)
            {
                var curArg = args[i];

                if (curArg.StartsWith("--"))
                {
                    string curVal = null;
                    int dex = curArg.IndexOf('=');

                    if (dex >= 0)
                    {
                        curVal = curArg.Substring(dex + 1);
                        curArg = curArg.Substring(0, dex);
                    }

                    ClParam curParam;
                    if (!stringDict.TryGetValue(curArg, out curParam))
                    {
                        Console.Error.WriteLine(TextResources.ParamUnknown, curArg);
                        continue;
                    }

                    curParam.Key = curArg;

                    if (curParam.TakesValue)
                    {
                        if (string.IsNullOrEmpty(curVal))
                        {
                            if (i != end)
                            {
                                int nextI = i + 1;
                                curVal = args[nextI];

                                if (curVal.StartsWith("-"))
                                    curVal = null;
                                else
                                    i = nextI;
                            }
                        }

                        if (string.IsNullOrEmpty(curVal))
                        {
                            Console.Error.WriteLine(TextResources.ParamReqArg, curArg);
                            result = true;
                            continue;
                        }
                    }

                    if (curVal != null && curParam.SetValue(curVal, orderedParams.Count))
                        return true;

                    curParam.IsSet = true;

                    if (GetBadParams(curParam))
                    {
                        result = true;
                        continue;
                    }

                    _setParams.Add(curParam);
                    orderedParams.Add(curParam);

                    continue;
                }

                if (curArg.StartsWith("-"))
                {
                    char prevChar = '-', curChar;

                    if (curArg.Length == 1)
                        continue;

                    int curEnd = curArg.Length - 1;

                    for (int j = 1; j < curArg.Length; j++, prevChar = curChar)
                    {
                        curChar = curArg[j];

                        ClParam curParam;

                        if (curChar == '=')
                        {
                            if (prevChar == '-')
                            {
                                Console.Error.WriteLine(TextResources.ParamInvalid, curArg);
                                return true;
                            }

                            if (charDict.TryGetValue(prevChar, out curParam))
                            {
                                if (curParam.TakesValue)
                                    break;

                                Console.Error.WriteLine(TextResources.ParamNoArg, "-" + curParam.ShortName);
                                return true;
                            }
                            break;
                        }

                        if (curChar == '-')
                        {
                            curArg = curArg.Substring(j);
                            curEnd = curArg.Length - 1;
                            continue;
                        }

                        if (!char.IsLetter(curChar))
                        {
                            Console.Error.WriteLine(TextResources.ParamInvalid, curArg);
                            return true;
                        }

                        if (!charDict.TryGetValue(curChar, out curParam))
                        {
                            Console.Error.WriteLine(TextResources.ParamUnknown, "-" + curChar);
                            continue;
                        }

                        string curKey = "-" + curChar;
                        curParam.Key = curKey;

                        string curVal = null;
                        if (j == curEnd)
                        {
                            int nextI = i + 1;
                            if (nextI == args.Length || !curParam.TakesValue || (curVal = args[nextI]).StartsWith("-"))
                                curVal = null;
                            else
                                i = nextI;
                        }
                        else
                        {
                            if (curArg[j + 1] == '=')
                                curVal = curArg.Substring(j + 2);
                        }

                        if (string.IsNullOrEmpty(curVal))
                        {
                            if (curParam.TakesValue)
                            {
                                Console.Error.WriteLine(TextResources.ParamReqArg, curKey);
                                result = true;
                                continue;
                            }
                        }
                        else if (curParam.SetValue(curVal, orderedParams.Count))
                        {
                            result = true;
                            continue;
                        }

                        curParam.IsSet = true;
                        if (GetBadParams(curParam))
                        {
                            result = true;
                            continue;
                        }

                        _setParams.Add(curParam);
                        orderedParams.Add(curParam);
                    }

                    continue;
                }

                _rawParam.Key = string.Empty;
                if (_rawParam.SetValue(curArg, orderedParams.Count))
                    return true;
                _rawParam.IsSet = true;
                _setParams.Add(_rawParam);
                orderedParams.Add(_rawParam);
            }
            if (!result)
                _orderedParams = orderedParams.ToArray();
            _disposed = true;
            return result;
        }

        private ClParam[] _orderedParams;
        public ClParam[] OrderedParams { get { return (ClParam[])_orderedParams?.Clone(); } }

        private void CurParam_SetChanged(object sender, EventArgs e)
        {
            ClParam curParam = (ClParam)sender;
            if (curParam.IsSet)
                _setParams.Add(curParam);
            else
                _setParams.Remove(curParam);
        }

        private bool GetBadParams(ClParam curParam)
        {
            ClParam[] badParams = curParam.MutualExclusives.Where(p => _setParams.Contains(p) && !_badParams.Contains(p))
                .Concat(_setParams.Where(p => p.MutualExclusives.Contains(curParam) && !_badParams.Contains(p))).Distinct().ToArray();

            if (badParams.Length == 0) return false;

            HashSet<ClParam> seenParams = new HashSet<ClParam>();

            _badParams.Add(curParam);

            for (int i = 0; i < badParams.Length; i++)
            {
                Func<ClParam, string> message;
                var cParam = badParams[i];
                _badParams.Add(cParam);
                if (cParam.OtherMessages.TryGetValue(curParam, out message))
                {
                    Console.Error.WriteLine(message(curParam));
                    continue;
                }

                if (curParam.OtherMessages.TryGetValue(cParam, out message))
                {
                    Console.Error.WriteLine(message(cParam));
                    continue;
                }
                seenParams.Add(cParam);
            }

            if (seenParams.Count != 0)
                Console.Error.WriteLine(TextResources.MutuallyExclusive, string.Join(", ", badParams.Concat(new ClParam[] { curParam }).Select(i => i.Key)));

            return true;
        }

        private bool _disposed;
        public void Dispose()
        {
            _disposed = true;
            if (_params == null) return;
            _params.Clear();
            _setParams.Clear();
            _params = null;
            _rawParam = null;
            _setParams.Clear();
        }
    }

    internal abstract class ClParam
    {
        protected ClParam(ClParser parser, string helpMessage, char shortName, params string[] longNames)
        {
            Parser = parser;

            parser.Params.Add(this);

            ShortName = shortName;
            LongNames = longNames == null ? new string[0] : longNames.Select(i => i.Trim('-', ' ').Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            HelpMessage = helpMessage;
        }

        public readonly ClParser Parser;

        public readonly string HelpMessage;

        public readonly char ShortName;

        public readonly string[] LongNames;

        public abstract bool TakesValue { get; }

        private int _index = -1;
        public int Index
        {
            get { return _index; }
            protected set { _index = value; }
        }

        public abstract bool SetValue(string value, int index);

        private bool _isSet;
        public bool IsSet
        {
            get { return _isSet; }
            set
            {
                if (value != _isSet)
                {
                    if (SetChanged != null)
                        SetChanged(this, EventArgs.Empty);
                    _isSet = value;
                }
            }
        }

        public event EventHandler SetChanged;

        private string _key;
        public string Key
        {
            get { return _key; }
            set
            {
                if (value == null || _key == null)
                    _key = value;
            }
        }

        public HashSet<ClParam> MutualExclusives = new HashSet<ClParam>();

        public Dictionary<ClParam, Func<ClParam, string>> OtherMessages = new Dictionary<ClParam, Func<ClParam, string>>();

        public override string ToString()
        {
            if (ShortName == '\0')
            {
                if (LongNames.Length == 0)
                    return base.ToString();
                return "--" + LongNames[0];
            }

            string shortName = "-" + ShortName;
            if (LongNames.Length == 0) return shortName;

            return shortName + ", --" + LongNames[0];
        }
    }

    internal abstract class ClParamValueBase : ClParam
    {
        public ClParamValueBase(ClParser parser, string helpMessage, string argName, char shortName, params string[] longNames)
            : base(parser, helpMessage, shortName, longNames)
        {
            ArgName = argName;
        }

        public readonly string ArgName;

        public override bool TakesValue
        {
            get { return true; }
        }
    }

    internal class ClParamValue : ClParamValueBase
    {
        public ClParamValue(ClParser parser, string helpMessage, string argName, char shortName, params string[] longNames)
            : base(parser, helpMessage, argName, shortName, longNames)
        {
        }

        public string Value;

        public Func<string, string> ConvertValue;

        public override bool SetValue(string value, int index)
        {
            if (ConvertValue != null && value != null)
                value = ConvertValue(value);

            if (Value != null && value != null && value != Value)
            {
                if (Key.Length == 0)
                    Console.Error.WriteLine(TextResources.ParamDupLit, value);
                else
                    Console.Error.WriteLine(TextResources.ParamDup, Key, value);
                return true;
            }

            Index = index;
            Value = value;
            return false;
        }

        public override string ToString()
        {
            if (IsSet)
            {
                if (!string.IsNullOrEmpty(Value))
                    return Key + "=" + Value;
                else
                    return Key;
            }

            return base.ToString();
        }
    }

    internal class ClParamFlag : ClParam
    {
        public ClParamFlag(ClParser parser, string helpMessage, char shortName, params string[] longNames)
            : base(parser, helpMessage, shortName, longNames)
        {
        }

        public override bool TakesValue
        {
            get { return false; }
        }

        public override bool SetValue(string value, int index)
        {
            Console.Error.WriteLine(TextResources.ParamNoArg, Key);
            return false;
        }
    }

    internal class ClParamMulti : ClParamValueBase
    {
        public ClParamMulti(ClParser parser, string helpMessage, string argName, char shortName, params string[] longNames)
            : base(parser, helpMessage, argName, shortName, longNames)
        {
            _indices = new Dictionary<int, IndexedString>();
            _indicesRO = new ReadOnlyDictionary<int, IndexedString>(_indices);
        }

        public override bool TakesValue
        {
            get { return true; }
        }

        private List<IndexedString> _vals = new List<IndexedString>();

        public IndexedString[] Values { get { return _vals.ToArray(); } }

        private readonly Dictionary<int, IndexedString> _indices;
        private readonly ReadOnlyDictionary<int, IndexedString> _indicesRO;
        public ReadOnlyDictionary<int, IndexedString> ValuesByIndex { get { return _indicesRO; } }

        public override bool SetValue(string value, int index)
        {
            var indexedValue = new IndexedString(value, index);

            _vals.Add(indexedValue);
            _indices.Add(index, indexedValue);
            if (Index < 0)
                Index = index;
            return false;
        }
    }

    internal class ClParamEnum<TEnum> : ClParamValueBase
        where TEnum : struct
    {
        public ClParamEnum(ClParser parser, string helpMessage, Dictionary<string, TEnum> locArgs, Dictionary<string, TEnum> unArgs, char shortName, params string[] longNames)
            : base(parser, helpMessage, string.Join("|", locArgs.Keys), shortName, longNames)
        {
            _args = new Dictionary<string, TEnum>(unArgs, StringComparer.OrdinalIgnoreCase);
            foreach (var curKVP in locArgs)
            {
                if (!_args.ContainsKey(curKVP.Key))
                    _args.Add(curKVP.Key, curKVP.Value);
            }
        }

        private Dictionary<string, TEnum> _args;

        public TEnum? Value;
        public string StrValue;

        public override bool SetValue(string value, int index)
        {
            if (value == null)
            {
                Value = null;
                return false;
            }
            value = value.Trim();

            TEnum newValue;

            if (!_args.TryGetValue(value, out newValue))
            {
                Console.Error.WriteLine(TextResources.BadEnumValue, value, Key);
                return true;
            }

            if (!Value.HasValue)
            {
                StrValue = value;
            }
            else if (!Value.Value.Equals(newValue))
            {
                Console.Error.WriteLine(TextResources.ParamDup, Key, value);
                return true;
            }
            Value = newValue;
            return false;
        }

        public override string ToString()
        {
            if (IsSet)
            {
                if (Value.HasValue)
                    return Key + "=" + Value.Value;
                else
                    return Key;
            }

            return base.ToString();
        }
    }

    internal struct IndexedString : IEquatable<IndexedString>, IComparable<IndexedString>
    {
        public IndexedString(string value, int index)
        {
            _value = value;
            _index = index;
        }

        private int _index;
        public int Index { get { return _index; } }

        private string _value;
        public string Value { get { return _value; } }

        public override string ToString()
        {
            return string.Format("[{0}], {{{1}}}", _index, _value);
        }

        #region Comparison
        public int CompareTo(IndexedString other)
        {
            int comp = _index.CompareTo(other._index);
            if (comp != 0) return comp;

            return string.CompareOrdinal(_value, other._value);
        }

        public static bool operator <(IndexedString x, IndexedString y)
        {
            return x.CompareTo(y) < 0;
        }

        public static bool operator >(IndexedString x, IndexedString y)
        {
            return x.CompareTo(y) > 0;
        }

        public static bool operator <=(IndexedString x, IndexedString y)
        {
            return x.CompareTo(y) <= 0;
        }

        public static bool operator >=(IndexedString x, IndexedString y)
        {
            return x.CompareTo(y) >= 0;
        }
        #endregion

        #region Equality
        public bool Equals(IndexedString other)
        {
            return _index == other._index && _value == other._value;
        }

        public override bool Equals(object obj)
        {
            return obj is IndexedString && Equals((IndexedString)obj);
        }

        public override int GetHashCode()
        {
            if (_value == null) return _index;
            return _index + _value.GetHashCode();
        }

        public static bool operator ==(IndexedString x, IndexedString y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(IndexedString x, IndexedString y)
        {
            return !x.Equals(y);
        }
        #endregion

        internal static IndexedString Selector(string s)
        {
            return new IndexedString(s, -1);
        }
    }
}
