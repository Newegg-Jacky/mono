//
// OptionDetails.cs
//
// Author: Rafael Teixeira (rafaelteixeirabr@hotmail.com)
//
// (C) 2002 Rafael Teixeira
//

using System;
using System.Collections;
using System.IO;
using System.Reflection;

namespace Mono.GetOptions
{
	public enum WhatToDoNext
	{
		AbandonProgram,
		GoAhead
	}
	
	internal enum OptionProcessingResult
	{
		NotThisOption,
		OptionAlone,
		OptionConsumedParameter
	}

	internal class OptionDetails : IComparable
	{
		public string ShortForm;
		public string LongForm;
		public string AlternateForm;
		public string ShortDescription;
		public bool NeedsParameter;
		public int MaxOccurs; // negative means there is no limit
		public int Occurs;
		public bool BooleanOption;
		public Options OptionBundle;
		public MemberInfo MemberInfo;
		public ArrayList Values;
		public System.Type ParameterType;
		public string paramName = null;
		public string ParamName 
		{
			get 
			{ 
				if (paramName == null)
				{
					int whereBegins = ShortDescription.IndexOf("{");
					if (whereBegins < 0)
						paramName = "PARAM";
					else
					{
						int whereEnds = ShortDescription.IndexOf("}");
						if (whereEnds < whereBegins)
							whereEnds = ShortDescription.Length+1;
						
						paramName = ShortDescription.Substring(whereBegins + 1, whereEnds - whereBegins - 1);
						ShortDescription = 
							ShortDescription.Substring(0, whereBegins) + 
							paramName +
							ShortDescription.Substring(whereEnds + 1);
					}
				}
				return paramName;
			}
		}
				
		public static bool Verbose = false;
		
		public override string ToString()
		{
			string optionHelp;
			// TODO: Yet not that good
			string shortPrefix;
			string longPrefix;
			bool hasLongForm = (this.LongForm != null && this.LongForm != string.Empty);
			if(this.OptionBundle.ParsingMode == Mono.GetOptions.OptionsParsingMode.Windows)
			{
				shortPrefix = "/";
				longPrefix = "/";
			} 
			else 
			{
				shortPrefix = "-";
				longPrefix = "--";
			}
			optionHelp = "  ";
			optionHelp += (this.ShortForm != string.Empty) ? shortPrefix+this.ShortForm+" " : "   ";
			optionHelp += hasLongForm ? longPrefix+this.LongForm : "";
			if (NeedsParameter)
			{
				if (hasLongForm)
					optionHelp += ":"; 
				optionHelp += ParamName; 
			}
			optionHelp = optionHelp.PadRight(32) + " ";
			optionHelp += this.ShortDescription;
			if (this.AlternateForm != string.Empty && this.AlternateForm != null)
				optionHelp += " [/"+this.AlternateForm + "]";
			return optionHelp; 
		}

		private static System.Type TypeOfMember(MemberInfo memberInfo)
		{
			if ((memberInfo.MemberType == MemberTypes.Field && memberInfo is FieldInfo))
				return ((FieldInfo)memberInfo).FieldType;

			if ((memberInfo.MemberType == MemberTypes.Property && memberInfo is PropertyInfo))
				return ((PropertyInfo)memberInfo).PropertyType;

			if ((memberInfo.MemberType == MemberTypes.Method && memberInfo is MethodInfo))
			{
				if (((MethodInfo)memberInfo).ReturnType.FullName != typeof(WhatToDoNext).FullName)
					throw new NotSupportedException("Option method must return '" + typeof(WhatToDoNext).FullName + "'");

				ParameterInfo[] parameters = ((MethodInfo)memberInfo).GetParameters();
				if ((parameters == null) || (parameters.Length == 0))
					return null;
				else
					return parameters[0].ParameterType;
			}

			throw new NotSupportedException("'" + memberInfo.MemberType + "' memberType is not supported");
		}

		public OptionDetails(MemberInfo memberInfo, OptionAttribute option, Options optionBundle)
		{
			this.ShortForm = ("" + option.ShortForm).Trim();
			this.LongForm = (option.LongForm == string.Empty)? memberInfo.Name:option.LongForm;
			this.AlternateForm = option.AlternateForm;
			this.ShortDescription = option.ShortDescription;
			this.Occurs = 0;
			this.OptionBundle = optionBundle; 
			this.BooleanOption = false;
			this.MemberInfo = memberInfo;
			this.NeedsParameter = false;
			this.Values = null;
			this.MaxOccurs = 1;
			this.ParameterType = TypeOfMember(memberInfo);

			if (this.ParameterType != null)
			{
				if (this.ParameterType.FullName != "System.Boolean")
				{
					if (this.LongForm.IndexOf(':') >= 0)
						throw new InvalidOperationException("Options with an embedded colon (':') in their visible name must be boolean!!! [" + 
									this.MemberInfo.ToString() + " isn't]");
				
					this.NeedsParameter = true;

					if (option.MaxOccurs != 1)
					{
						if (this.ParameterType.IsArray)
						{
							this.Values = new ArrayList();
							this.MaxOccurs = option.MaxOccurs;
						}
						else
						{
							if (this.MemberInfo is MethodInfo || this.MemberInfo is PropertyInfo)
								this.MaxOccurs = option.MaxOccurs;
							else
								throw new InvalidOperationException("MaxOccurs set to non default value (" + option.MaxOccurs + ") for a [" + 
											this.MemberInfo.ToString() + "] option");
						}
					}
				}
				else
				{
					this.BooleanOption = true;
					if (option.MaxOccurs != 1)
					{			
						if (this.MemberInfo is MethodInfo || this.MemberInfo is PropertyInfo)
							this.MaxOccurs = option.MaxOccurs;
						else
							throw new InvalidOperationException("MaxOccurs set to non default value (" + option.MaxOccurs + ") for a [" + 
										this.MemberInfo.ToString() + "] option");
					}
				}
			}
		}

		internal string Key
		{
			get { return this.ShortForm + this.LongForm; }
		}

		int IComparable.CompareTo(object other)
		{
			return Key.CompareTo(((OptionDetails)other).Key);
		}

		public void TransferValues()
		{
			if (Values != null)
			{
				if (MemberInfo is FieldInfo)
				{
					((FieldInfo)MemberInfo).SetValue(OptionBundle, Values.ToArray(ParameterType.GetElementType()));
					return;
				}

				if (MemberInfo is PropertyInfo) 
				{
					((PropertyInfo)MemberInfo).SetValue(OptionBundle, Values.ToArray(ParameterType.GetElementType()), null);
					return;
				}

				if ((WhatToDoNext)((MethodInfo)MemberInfo).Invoke(OptionBundle, new object[] { Values.ToArray(ParameterType.GetElementType()) }) == WhatToDoNext.AbandonProgram)
					System.Environment.Exit(1);
			}
		}

		private void Occurred(int howMany)
		{
			Occurs += howMany;

			if (MaxOccurs > 0 && Occurs > MaxOccurs)
				throw new IndexOutOfRangeException("Option " + ShortForm + " can be used at most " + MaxOccurs + " times");
		}

		private void DoIt(bool setValue)
		{
			if (!NeedsParameter)
			{
				Occurred(1);

				if (Verbose)
					Console.WriteLine("<" + this.LongForm + "> set to [true]");

				if (MemberInfo is FieldInfo)
				{
					((FieldInfo)MemberInfo).SetValue(OptionBundle, setValue);
					return;
				}
				if (MemberInfo is PropertyInfo)
				{
					((PropertyInfo)MemberInfo).SetValue(OptionBundle, setValue, null);
					return;
				}
				if ((WhatToDoNext)((MethodInfo)MemberInfo).Invoke(OptionBundle, null) == WhatToDoNext.AbandonProgram)
					System.Environment.Exit(1);

				return;
			}
		}
		
		private void DoIt(string parameterValue)
		{
			if (parameterValue == null)
				parameterValue = "";

			string[] parameterValues = parameterValue.Split(',');

			Occurred(parameterValues.Length);

			foreach (string parameter in parameterValues)
			{

				object convertedParameter = null;

				if (Verbose)
					Console.WriteLine("<" + this.LongForm + "> set to [" + parameter + "]");

				if (Values != null && parameter != null)
				{
					convertedParameter = Convert.ChangeType(parameter, ParameterType.GetElementType());
					Values.Add(convertedParameter);
					continue;
				}

				if (parameter != null)
					convertedParameter = Convert.ChangeType(parameter, ParameterType);

				if (MemberInfo is FieldInfo)
				{
					((FieldInfo)MemberInfo).SetValue(OptionBundle, convertedParameter);
					continue;
				}

				if (MemberInfo is PropertyInfo)
				{
					((PropertyInfo)MemberInfo).SetValue(OptionBundle, convertedParameter, null);
					continue;
				}

				if ((WhatToDoNext)((MethodInfo)MemberInfo).Invoke(OptionBundle, new object[] { convertedParameter }) == WhatToDoNext.AbandonProgram)
					System.Environment.Exit(1);
			}
		}

		private bool IsThisOption(string arg)
		{
			if (arg != null && arg != string.Empty)
			{
				arg = arg.TrimStart('-', '/');				
				return (arg == ShortForm || arg == LongForm || arg == AlternateForm);
			}
			return false;
		}

		public OptionProcessingResult ProcessArgument(string arg, string nextArg)
		{
			if (IsThisOption(arg))
			{
				if (!NeedsParameter)
				{
					DoIt(true); // in preparation for vbc-like booleans
					return OptionProcessingResult.OptionAlone;
				}
				else
				{
					DoIt(nextArg);
					return OptionProcessingResult.OptionConsumedParameter;
				}
			}

			if (IsThisOption(arg + ":" + nextArg))
			{
				DoIt(true);
				return OptionProcessingResult.OptionConsumedParameter;
			}

			return OptionProcessingResult.NotThisOption;
		}
	}
}
