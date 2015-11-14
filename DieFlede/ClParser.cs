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
using System.Linq;
using DieFledermaus.Cli.Globalization;

namespace DieFledermaus.Cli
{

    internal class ClParser : IDisposable
    {
        private List<ClParam> _params = new List<ClParam>();
        public List<ClParam> Params { get { return _params; } }

        public ClParser(int rawIndex, params ClParam[] parameters)
        {
            _rawParam = parameters[rawIndex];
            _params.AddRange(parameters);
        }

        private ClParam _rawParam;

        public ClParam RawParam
        {
            get { return _rawParam; }
            set
            {
                if (_disposed)
                    throw new ObjectDisposedException(null);
                if (value == null)
                    throw new ArgumentNullException("value");
                _rawParam = value;
            }
        }

        private HashSet<ClParam> _setParams = new HashSet<ClParam>();

        public bool Parse(string[] args)
        {
            if (_disposed)
                throw new ObjectDisposedException(null);

            Dictionary<string, ClParam> stringDict = new Dictionary<string, ClParam>(_params.Count, StringComparer.OrdinalIgnoreCase);
            Dictionary<char, ClParam> charDict = new Dictionary<char, ClParam>(_params.Count);

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
                        curArg = curArg.Substring(dex);
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
                            return true;
                        }
                    }

                    if (curVal != null && curParam.SetValue(curVal))
                        return true;

                    curParam.IsSet = true;
                    if (curParam.SetAction != null && curParam.SetAction(this))
                        return true;

                    if (GetBadParams(_setParams, curParam))
                        return true;

                    _setParams.Add(curParam);

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

                        curParam.Key = "-" + curChar;

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
                                Console.Error.WriteLine(TextResources.ParamReqArg, curParam.Key);
                                return true;
                            }
                        }
                        else if (curParam.SetValue(curVal))
                            return true;

                        curParam.IsSet = true;
                        if (curParam.SetAction != null && curParam.SetAction(this))
                            return true;
                        if (GetBadParams(_setParams, curParam))
                            return true;

                        _setParams.Add(curParam);
                    }

                    continue;
                }

                _rawParam.Key = string.Empty;
                if (_rawParam.SetValue(curArg))
                    return true;
                _rawParam.IsSet = true;
                _setParams.Add(_rawParam);
            }
            _disposed = true;
            return false;
        }

        private void CurParam_SetChanged(object sender, EventArgs e)
        {
            ClParam curParam = (ClParam)sender;
            if (curParam.IsSet)
                _setParams.Add(curParam);
            else
                _setParams.Remove(curParam);
        }

        private static bool GetBadParams(HashSet<ClParam> setParams, ClParam curParam)
        {
            ClParam[] badParams = curParam.MutualExclusives.Where(p => setParams.Contains(p))
                .Concat(setParams.Where(p => p.MutualExclusives.Contains(curParam))).ToArray();

            if (badParams.Length == 0) return false;

            for (int i = 0; i < badParams.Length; i++)
            {
                Func<ClParam, string> message;
                var cParam = badParams[i];
                if (cParam.OtherMessages.TryGetValue(curParam, out message))
                {
                    Console.Error.WriteLine(message(curParam));
                    return true;
                }

                if (curParam.OtherMessages.TryGetValue(cParam, out message))
                {
                    Console.Error.WriteLine(message(cParam));
                    return true;
                }
            }

            Console.Error.WriteLine(TextResources.MutuallyExclusive, string.Join(", ", badParams.Select(i => i.Key)));

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

    internal class ClParam
    {
        public ClParam(char shortName, params string[] longNames)
        {
            ShortName = shortName;
            LongNames = longNames ?? new string[0];
        }

        public ClParam(params string[] longNames)
            : this('\0', longNames)
        {
        }

        public ClParam(bool hasValue, char shortName, params string[] longNames)
            : this(shortName, longNames)
        {
            TakesValue = SetOnce = hasValue;
        }

        public ClParam(bool hasValue, params string[] longNames)
            : this(hasValue, '\0', longNames)
        {
        }

        public readonly char ShortName;

        public readonly string[] LongNames;

        public readonly bool TakesValue, SetOnce;

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

        public Func<string, string> ConvertValue;

        public Func<ClParser, bool> SetAction;

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

        public string Value;

        public virtual bool SetValue(string value)
        {
            if (!TakesValue && value != null)
            {
                Console.Error.WriteLine(TextResources.ParamNoArg, _key);
                return false;
            }

            if (ConvertValue != null && value != null)
                value = ConvertValue(value);

            if (Value != null && value != null && !value.Equals(Value, StringComparison.Ordinal))
            {
                if (_key.Length == 0)
                    Console.Error.WriteLine(TextResources.ParamDupLit, value);
                else
                    Console.Error.WriteLine(TextResources.ParamDup, _key, value);
                return true;
            }

            Value = value;
            return false;
        }

        public HashSet<ClParam> MutualExclusives = new HashSet<ClParam>();

        public Dictionary<ClParam, Func<ClParam, string>> OtherMessages = new Dictionary<ClParam, Func<ClParam, string>>();

        public override string ToString()
        {
            if (Value != null)
                return _key + "=" + Value;
            else if (_isSet)
                return _key;

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
}
