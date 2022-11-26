﻿using CodegenCS.___InternalInterfaces___;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using static InterpolatedColorConsole.Symbols;
using System.Threading.Tasks;
using CodegenCS.Utils;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Invocation;
using System.CommandLine.Help;
using System.CommandLine.IO;
using static CodegenCS.Utils.TypeUtils;
using CodegenCS.Runtime;
using CodegenCS.Models;
using Newtonsoft.Json;

namespace CodegenCS.TemplateLauncher
{
    public class TemplateLauncher
    {
        protected ILogger _logger;
        protected ICodegenContext _ctx;
        protected int _expectedModels;
        protected Type _model1Type = null;
        protected Type _model2Type = null;
        protected Type _entryPointClass = null;
        protected FileInfo _templateFile;
        internal FileInfo _originallyInvokedTemplateFile;
        public bool ShowTemplateHelp { get; set; }
        protected string _defaultOutputFile = null;
        protected MethodInfo _entryPointMethod = null;
        protected FileInfo[] _modelFiles = null;
        protected Type _iTypeTemplate = null;
        protected string _outputFolder = null;
        protected string _executionFolder = null;
        public bool VerboseMode { get; set; }
        public Func<BindingContext, HelpBuilder> HelpBuilderFactory = null;
        public delegate ParseResult ParseCliUsingCustomCommandDelegate(string filePath, Type model1Type, Type model2Type, DependencyContainer dependencyContainer, MethodInfo configureCommand, ParseResult parseResult);
        public ParseCliUsingCustomCommandDelegate ParseCliUsingCustomCommand = null;
        protected IModelFactory _modelFactory;
        protected DependencyContainer _dependencyContainer;

        public TemplateLauncher(ILogger logger, ICodegenContext ctx, DependencyContainer parentDependencyContainer, bool verboseMode)
        {
            _logger = logger;
            _ctx = ctx;
            VerboseMode = verboseMode;
            _dependencyContainer = new DependencyContainer().AddModelFactory();
            _dependencyContainer.ParentContainer = parentDependencyContainer;
            _modelFactory = _dependencyContainer.Resolve<IModelFactory>();
        }

        /// <summary>
        /// Template Launcher options. For convenience this same model is also used for CLI options parsing.
        /// </summary>
        public class TemplateLauncherArgs
        {
            /// <summary>
            /// Path for Template (DLL that will be executed)
            /// </summary>
            public string Template { get; set; }
            
            /// <summary>
            /// Path for the Models (if any)
            /// </summary>
            public string[] Models { get; set; } = new string[0];

            /// <summary>
            /// Output folder for output files. If not defined will default to Current Directory.
            /// This is just a "base" path but output files may define their relative locations at/under/above this base path.
            /// </summary>
            public string OutputFolder { get; set; }

            /// <summary>
            /// Folder where execution will happen. Before template is executed the current folder is changed to this (if defined).
            /// Useful for loading files (e.g. IModelFactory.LoadModelFromFile{TModel}(path)) with relative paths.
            /// </summary>
            public string ExecutionFolder { get; set; }

            /// <summary>
            /// DefaultOutputFile. If not defined will be based on the Template DLL path, adding CS extension.
            /// e.g. for "Template.dll" the default output file will be "Template.cs".
            /// </summary>
            public string DefaultOutputFile { get; set; }

            public string[] TemplateSpecificArguments { get; set; } = new string[0];
        }


        public async Task<int> LoadAndExecuteAsync(TemplateLauncherArgs args, ParseResult parseResult)
        {
            var loadResult = await LoadAsync(args.Template);
            if (loadResult.ReturnCode != 0)
                return loadResult.ReturnCode;
            return await ExecuteAsync(args, parseResult);
        }

        public class TemplateLoadResponse
        {
            public int ReturnCode { get; set; }
            public Type Model1Type { get; set; }
            public Type Model2Type { get; set; }
        }

        public async Task<TemplateLoadResponse> LoadAsync(string templateDll)
        {
            if (!((_templateFile = new FileInfo(templateDll)).Exists || (_templateFile = new FileInfo(templateDll + ".dll")).Exists))
            {
                await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"ERROR: Cannot find Template DLL {templateDll}");
                return new TemplateLoadResponse() { ReturnCode = -1 };
            }

            await _logger.WriteLineAsync(ConsoleColor.Green, $"Loading {ConsoleColor.Yellow}'{_templateFile.Name}'{PREVIOUS_COLOR}...");


            Type templatingInterface = null;

            var asm = Assembly.LoadFile(_templateFile.FullName);

            if (asm.GetName().Version?.ToString() != "0.0.0.0")
                await _logger.WriteLineAsync($"{ConsoleColor.Cyan}{_templateFile.Name}{PREVIOUS_COLOR} version {ConsoleColor.Cyan}{asm.GetName().Version}{PREVIOUS_COLOR}");

            var asmTypes = asm.GetTypes().ToList();
            var asmMethods = asmTypes.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)).ToList();


            // If a class implements multiple templating interfaces (or if different classs implement different interfaces) we'll pick the most elaborated one
            var interfacesPriority = new Type[]
            {
                typeof(ICodegenMultifileTemplate<,>),
                typeof(ICodegenMultifileTemplate<>),
                typeof(ICodegenMultifileTemplate),

                typeof(ICodegenTemplate<,>),
                typeof(ICodegenTemplate<>),
                typeof(ICodegenTemplate),

                typeof(ICodegenStringTemplate<,>),
                typeof(ICodegenStringTemplate<>),
                typeof(ICodegenStringTemplate),
            };

            // First we search for a single "Main" method.
            if (_entryPointMethod == null && asmMethods.Where(m => m.Name == "Main").Count() == 1)
            {
                _entryPointMethod = asmMethods.Where(m => m.Name == "Main").Single();
                _entryPointClass = _entryPointMethod.DeclaringType;
            }

            // LEGACY ? Maybe we should use the Main() standard
            // Then we search for templating interfaces (they all inherit from IBaseTemplate)
            if (_entryPointClass == null)
            {
                var templateInterfaceTypes = asmTypes.Where(t => typeof(IBaseTemplate).IsAssignableFrom(t)).ToList();

                // If there's a single class implementing IBaseTemplate
                if (_entryPointClass == null && templateInterfaceTypes.Count() == 1)
                {
                    _entryPointClass = templateInterfaceTypes.Single();
                    // Pick the best interface if it implements multiple
                    for (int i = 0; i < interfacesPriority.Length && templatingInterface == null; i++)
                    {
                        if (IsAssignableToType(_entryPointClass, interfacesPriority[i]))
                            templatingInterface = interfacesPriority[i];
                    }
                }
                else if (templateInterfaceTypes.Any()) // if multiple classes, find the best one by the best interface
                {
                    for (int i = 0; i < interfacesPriority.Length && _entryPointClass == null; i++)
                    {
                        IEnumerable<Type> types2;
                        if ((types2 = templateInterfaceTypes.Where(t => IsAssignableToType(t, interfacesPriority[i]))).Count() == 1)
                        {
                            _entryPointClass = types2.Single();
                            templatingInterface = interfacesPriority[i];
                        }
                    }
                }
            }

            if (_entryPointMethod == null && templatingInterface != null)
                _entryPointMethod = templatingInterface.GetMethod("Render", BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.Public);


            if (_entryPointClass == null || _entryPointMethod == null)
            {
                await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"ERROR: Could not find template entry-point in '{(_originallyInvokedTemplateFile ?? _templateFile).Name}'.");
                await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"Template should have a method called Main() or should implement ICodegenTemplate, ICodegenMultifileTemplate or ICodegenStringTemplate.");
                return new TemplateLoadResponse() { ReturnCode = -1 };
            }

            await _logger.WriteLineAsync(ConsoleColor.Cyan, $"Template entry-point: {ConsoleColor.White}'{_entryPointClass.Name}.{_entryPointMethod.Name}()'{PREVIOUS_COLOR}...");

            if (templatingInterface != null)
            {
                Type iBaseXModelTemplate = null;

                if (IsAssignableToType(templatingInterface, typeof(IBase1ModelTemplate<>)))
                    iBaseXModelTemplate = typeof(IBase1ModelTemplate<>);
                else if (IsAssignableToType(templatingInterface, typeof(IBase2ModelTemplate<,>)))
                    iBaseXModelTemplate = typeof(IBase2ModelTemplate<,>);
                else if (IsAssignableToType(templatingInterface, typeof(IBase0ModelTemplate)))
                    iBaseXModelTemplate = typeof(IBase0ModelTemplate);
                else
                    throw new NotImplementedException();

                if (IsAssignableToType(templatingInterface, typeof(IBaseMultifileTemplate)))
                    _iTypeTemplate = typeof(IBaseMultifileTemplate);
                else if (IsAssignableToType(templatingInterface, typeof(IBaseSinglefileTemplate)))
                    _iTypeTemplate = typeof(IBaseSinglefileTemplate);
                else if (IsAssignableToType(templatingInterface, typeof(IBaseStringTemplate)))
                    _iTypeTemplate = typeof(IBaseStringTemplate);
                else
                    throw new NotImplementedException();


                if (iBaseXModelTemplate == typeof(IBase2ModelTemplate<,>))
                {
                    _expectedModels = 2;
                    _model1Type = _entryPointClass.GetInterfaces().Where(it => it.IsGenericType && it.GetGenericTypeDefinition() == iBaseXModelTemplate).ToList()[0].GetGenericArguments()[0];
                    _model2Type = _entryPointClass.GetInterfaces().Where(it => it.IsGenericType && it.GetGenericTypeDefinition() == iBaseXModelTemplate).ToList()[0].GetGenericArguments()[1];
                }
                else if (iBaseXModelTemplate == typeof(IBase1ModelTemplate<>))
                {
                    _expectedModels = 1;
                    _model1Type = _entryPointClass.GetInterfaces().Where(it => it.IsGenericType && it.GetGenericTypeDefinition() == iBaseXModelTemplate).ToList()[0].GetGenericArguments()[0];
                }
                else
                    _expectedModels = 0;
            }
            else
            {
                // Models required by the entry point method 
                var entrypointMethodModels = _entryPointMethod.GetParameters().Select(p => p.ParameterType).Where(p => _modelFactory.CanCreateModel(p)).ToList();

                // Models required by the entrypoint class constructors
                var ctorsWithModelParameters = _entryPointClass.GetConstructors().Select(ctor => new
                {
                    ctor,
                    ModelArgs = ctor.GetParameters().Select(p => p.ParameterType).Where(p => _modelFactory.CanCreateModel(p)).ToList()
                });

                // if entrypoint method requires models, class should have at least one constructor NOT expecting any models.
                if (entrypointMethodModels.Any())
                {
                    if (ctorsWithModelParameters.Any(c => c.ModelArgs.Any()))
                    {
                        await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"ERROR: Templates with Main() entry-point may receive models in the entry-point or in the constructor, but not in both.");
                        return new TemplateLoadResponse() { ReturnCode = -1 };
                    }

                    if (entrypointMethodModels.GroupBy(t => t).Max(g => g.Count()>1))
                    {
                        await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"ERROR: Templates with Main() entry-point cannot receive 2 models of the same type.");
                        return new TemplateLoadResponse() { ReturnCode = -1 };
                    }

                    _expectedModels = entrypointMethodModels.Count();
                    if (_expectedModels >= 1)
                        _model1Type = entrypointMethodModels[0];
                    if (_expectedModels >= 2)
                        _model1Type = entrypointMethodModels[1];
                    if (_expectedModels >= 3)
                    {
                        await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"ERROR: Templates can expect a max of 2 models - but it's expecting {ConsoleColor.Yellow}'{string.Join("', '", entrypointMethodModels.Select(t => t.Name))}'{PREVIOUS_COLOR}");
                        return new TemplateLoadResponse() { ReturnCode = -1 };
                    }

                    return new TemplateLoadResponse() { ReturnCode = 0, Model1Type = _model1Type, Model2Type = _model2Type };
                }

                if (!ctorsWithModelParameters.Any(c => c.ModelArgs.Count() == 0))
                {
                    await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"ERROR: Templates with Main() entry-point may receive models in the entry-point or in the constructor, but not in both.");
                    return new TemplateLoadResponse() { ReturnCode = -1 };
                }

                // Models are in constructor. Let's pick the one with most models.
                var ctorAndParms = ctorsWithModelParameters.OrderByDescending(ctor => ctor.ModelArgs.Count()).First();

                if (ctorAndParms.ModelArgs.GroupBy(t => t).Select(g => g?.Count() ?? 0).Count() > 1)
                {
                    await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"ERROR: Templates with Main() entry-point cannot receive 2 models of the same type.");
                    return new TemplateLoadResponse() { ReturnCode = -1 };
                }

                _expectedModels = ctorAndParms.ModelArgs.Count();
                if (_expectedModels >= 1)
                    _model1Type = ctorAndParms.ModelArgs[0];
                if (_expectedModels >= 2)
                    _model1Type = ctorAndParms.ModelArgs[1];
                if (_expectedModels >= 3)
                {
                    await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"ERROR: Templates can expect a max of 2 models - but it's expecting {ConsoleColor.Yellow}'{string.Join("', '", ctorAndParms.ModelArgs.Select(t => t.Name))}'{PREVIOUS_COLOR}");
                    return new TemplateLoadResponse() { ReturnCode = -1 };
                }

            }

            return new TemplateLoadResponse() { ReturnCode = 0, Model1Type = _model1Type, Model2Type = _model2Type };
        }

        public async Task<int> ExecuteAsync(TemplateLauncherArgs _args, ParseResult parseResult)
        {
            if (_entryPointClass == null)
                throw new InvalidOperationException("Should call LoadAsync() before ExecuteAsync(). Or use LoadAndExecuteAsync()");

            _modelFiles = new FileInfo[_args.Models?.Length ?? 0];
            for (int i = 0; i < _modelFiles.Length; i++)
            {
                string model = _args.Models[i];
                if (model != null)
                {
                    if (!((_modelFiles[i] = new FileInfo(model)).Exists || (_modelFiles[i] = new FileInfo(model + ".json")).Exists || (_modelFiles[i] = new FileInfo(model + ".yaml")).Exists))
                    {
                        await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"ERROR: Cannot find find model {model}");
                        return -1;
                    }
                }
            }

            string providedTemplateName = _originallyInvokedTemplateFile?.Name ?? _templateFile?.Name ?? _args.Template;

            _outputFolder = _executionFolder = Directory.GetCurrentDirectory();
            _defaultOutputFile = Path.GetFileNameWithoutExtension(providedTemplateName) + ".generated.cs";
            if (!string.IsNullOrWhiteSpace(_args.OutputFolder))
                _outputFolder = Path.GetFullPath(_args.OutputFolder);
            if (!string.IsNullOrWhiteSpace(_args.ExecutionFolder))
                _executionFolder = Path.GetFullPath(_args.ExecutionFolder);
            if (!string.IsNullOrWhiteSpace(_args.DefaultOutputFile))
                _defaultOutputFile = _args.DefaultOutputFile;


            _ctx.DefaultOutputFile.RelativePath = _defaultOutputFile;

            _ctx.DependencyContainer.ParentContainer = _dependencyContainer;
            
            _dependencyContainer.RegisterSingleton<ILogger>(_logger);

            // CommandLineArgs can be injected in the template constructor (or in any dependency like "TemplateArgs" nested class), and will bring all command-line arguments that were not captured by dotnet-codegencs tool
            CommandLineArgs cliArgs = new CommandLineArgs(_args.TemplateSpecificArguments);
            _dependencyContainer.RegisterSingleton<CommandLineArgs>(cliArgs);



            #region If template has a method "public static void ConfigureCommand(Command)" then we can use it to parse the command-line arguments or to ShowHelp()
            // If template defines a method "public static void ConfigureCommand(Command command)", then this method can be used to configure (describe) the template custom Arguments and Options.
            // In this case dotnet-codegencs will pass an empty command to this configuration method, will create a Parser for the Command definition,
            // will parse the command line to extract/validate those extra arguments/options, and if there's any parse error it will invoke the regular ShowHelp() for the Command definition.
            // If there are no errors it will create and register (in the Dependency Injection container) ParseResult, BindingContext and InvocationContext.
            // Those objects (ParseResult/BindingContext/InvocationContext) can be used to get the args/options that the template needs.
            // Example: TemplateOptions constructor can take ParseResult and extract it's values using parseResult.CommandResult.GetValueForArgument and parseResult.CommandResult.GetValueForOption.

            var methods = _entryPointClass.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod);
            MethodInfo configureCommand = methods.Where(m => m.Name == "ConfigureCommand" && m.GetParameters().Count()==1 && m.GetParameters()[0].ParameterType == typeof(Command)).SingleOrDefault();

            if (configureCommand != null)
            {
                if (VerboseMode)
                {
                    await _logger.WriteLineAsync(ConsoleColor.DarkGray, $"{ConsoleColor.Yellow}ConfigureCommand(Command){PREVIOUS_COLOR} method was found.");
                    await _logger.WriteLineAsync(ConsoleColor.DarkGray, $"The following args will be forwarded to the template: {ConsoleColor.Yellow}'{string.Join("', '", _args.TemplateSpecificArguments)}'{PREVIOUS_COLOR}...");
                    await _logger.WriteLineAsync(ConsoleColor.DarkGray, $"These args can be injected into your template using {ConsoleColor.Yellow}CommandLineArgs{PREVIOUS_COLOR} class");
                    await _logger.WriteLineAsync(ConsoleColor.DarkGray, $"or using any custom class that implements {ConsoleColor.Yellow}IAutoBindCommandLineArgs{PREVIOUS_COLOR}");
                }
                if (ParseCliUsingCustomCommand != null)
                {
                    if (VerboseMode)
                        await _logger.WriteLineAsync(ConsoleColor.DarkGray, $"Parsing command-line arguments to check if they match the options/args defined in ConfigureCommand()...");
                    parseResult = ParseCliUsingCustomCommand(providedTemplateName, _model1Type, _model2Type, _ctx.DependencyContainer, configureCommand, parseResult);
                }
            }
            else
            {
                if (VerboseMode)
                {
                    await _logger.WriteLineAsync(ConsoleColor.DarkGray, $"{ConsoleColor.Yellow}ConfigureCommand(Command){PREVIOUS_COLOR} method was not found.");
                    await _logger.WriteLineAsync(ConsoleColor.DarkGray, $"The following args will be forwarded to the template: {ConsoleColor.Yellow}'{string.Join("', '", _args.TemplateSpecificArguments)}'{PREVIOUS_COLOR}...");
                    await _logger.WriteLineAsync(ConsoleColor.DarkGray, $"These args can be injected into your template using {ConsoleColor.Yellow}CommandLineArgs{PREVIOUS_COLOR} class.");
                }
            }


            BindingContext bindingContext = null;
            if (parseResult != null)
            {
                var invocationContext = new InvocationContext(parseResult);
                bindingContext = invocationContext.BindingContext;
                _dependencyContainer.RegisterSingleton<ParseResult>(parseResult);
                _dependencyContainer.RegisterSingleton<InvocationContext>(invocationContext);
                _dependencyContainer.RegisterSingleton<BindingContext>(bindingContext);
            }


            #endregion

            if (_expectedModels > _modelFiles.Count())
            {
                await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"ERROR: Template entry-point {ConsoleColor.White}'{_entryPointClass.Name}.{_entryPointMethod.Name}()'{PREVIOUS_COLOR} requires {ConsoleColor.White}{_expectedModels}{PREVIOUS_COLOR} model(s) but got only {ConsoleColor.White}{_modelFiles.Count()}{PREVIOUS_COLOR}.");

                if (parseResult != null)
                    ShowParseResults(bindingContext, parseResult);
                return -2;
            }
            if (_expectedModels < _modelFiles.Count())
            {
                await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"ERROR: Template entry-point {ConsoleColor.White}'{_entryPointClass.Name}.{_entryPointMethod.Name}()'{PREVIOUS_COLOR} requires {ConsoleColor.White}{_expectedModels}{PREVIOUS_COLOR} model(s) and got {ConsoleColor.White}{_modelFiles.Count()}{PREVIOUS_COLOR}.");
                await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"Maybe your template expects a model class and you forgot to use IInputModel interface? Or maybe you have provided an extra arg which is not expected?");

                if (parseResult != null)
                    ShowParseResults(bindingContext, parseResult);
                return -2;
            }

            if (parseResult != null && parseResult.Errors.Any())
            {
                foreach (var error in parseResult.Errors)
                    await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"ERROR: {error.Message}");
                ShowParseResults(bindingContext, parseResult);
                return -2;
            }

            if (ShowTemplateHelp)
            {
                ShowParseResults(bindingContext, parseResult);
                return -2;
            }



            #region Loading Required Models
            List<object> modelArgs = new List<object>();

            for (int i = 0; i < _expectedModels; i++)
            {
                Type modelType = null;
                try
                {
                    if (i == 0)
                        modelType = _model1Type;
                    else if (i == 1)
                        modelType = _model2Type;
                    modelType = modelType ?? _entryPointClass.GetInterfaces().Where(itf => itf.IsGenericType
                        && (itf.GetGenericTypeDefinition() == typeof(IBase1ModelTemplate<>) || itf.GetGenericTypeDefinition() == typeof(IBase2ModelTemplate<,>)))
                        .Select(interf => interf.GetGenericArguments().Skip(i).First()).Distinct().Single();
                    await _logger.WriteLineAsync(ConsoleColor.Cyan, $"Model{(_expectedModels > 1 ? (i + 1).ToString() : "")} type is {ConsoleColor.White}'{modelType.FullName}'{PREVIOUS_COLOR}...");
                }
                catch (Exception ex)
                {
                    await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"Could not get type for Model{(_expectedModels > 1 ? (i + 1).ToString() : "")}: {ex.Message}");
                    return -1;
                }
                try
                {
                    object model;
                    if (_iTypeTemplate != null) // old templating interfaces - always expect a JSON model
                        model = Newtonsoft.Json.JsonConvert.DeserializeObject(File.ReadAllText(_modelFiles[i].FullName), modelType);
                    else if (_modelFactory.CanCreateModel(modelType))
                    {
                        model = await _modelFactory.LoadModelFromFileAsync(modelType, _modelFiles[i].FullName);
                    }
                    else
                    {
                        await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"Could not load Model{(_expectedModels > 1 ? (i + 1).ToString() : "")}: Unknown type. Maybe your model should implement IJsonInputModel?");
                        return -1;
                    }
                    await _logger.WriteLineAsync(ConsoleColor.Cyan, $"Model{(_expectedModels > 1 ? (i + 1).ToString() : "")} successfuly loaded from {ConsoleColor.White}'{_modelFiles[i].Name}'{PREVIOUS_COLOR}...");
                    modelArgs.Add(model);
                    _dependencyContainer.RegisterSingleton(modelType, model); // might be injected both in _entryPointClass ctor or _entryPointMethod args
                }
                catch (Exception ex)
                {
                    await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"Could not load Model{(_expectedModels > 1 ? (i + 1).ToString() : "")} (type {ConsoleColor.White}'{modelType.FullName}'{PREVIOUS_COLOR}): {ex.Message}");
                    return -1;
                }
            }
            #endregion



            object instance = null;
            try
            {
                instance = _ctx.DependencyContainer.Resolve(_entryPointClass);

                //TODO: if _args.TemplateSpecificArguments?.Any() == true && typeof(CommandLineArgs) wasn't injected into the previous Resolve() call:
                // $"ERROR: There are unknown args but they couldn't be forwarded to the template because it doesn't define {ConsoleColor.Yellow}'ConfigureCommand(Command)'{PREVIOUS_COLOR} and doesn't take {ConsoleColor.White}CommandLineArgs{PREVIOUS_COLOR} in the constructor."
            }
            catch (CommandLineArgsException ex)
            {
                await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"{ex.Message}");
                if (VerboseMode)
                    await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"{ex.ToString()}");
                ex.ShowHelp(_logger);
                return -1;
            }
            catch (ArgumentException ex)
            {
                await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"{ex.Message}");
                if (VerboseMode)
                    await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"{ex.ToString()}");
                return -1;
            }

            string previousFolder = Directory.GetCurrentDirectory();
            try
            {
                if (_executionFolder != null)
                    Directory.SetCurrentDirectory(_executionFolder);

                JsonConvert.DefaultSettings = () => new JsonSerializerSettings
                {
                    Formatting = Newtonsoft.Json.Formatting.Indented,
                    ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
                };

                #region Invoking template
                if (_iTypeTemplate == typeof(IBaseMultifileTemplate)) // pass ICodegenContext
                {
                    modelArgs.Insert(0, _ctx);
                    (_entryPointClass.GetMethod(_entryPointMethod.Name, _entryPointMethod.GetParameters().Select(p => p.ParameterType).ToArray()) ?? _entryPointClass.GetMethod(_entryPointMethod.Name))
                        .Invoke(instance, modelArgs.ToArray());
                }
                else if (_iTypeTemplate == typeof(IBaseSinglefileTemplate)) // pass ICodegenTextWriter
                {
                    modelArgs.Insert(0, _ctx.DefaultOutputFile);
                    var method = _entryPointClass.GetMethod(_entryPointMethod.Name, _entryPointMethod.GetParameters().Select(p => p.ParameterType).ToArray());
                    method = method ?? _entryPointClass.GetMethod(_entryPointMethod.Name);
                    method.Invoke(instance, modelArgs.ToArray());
                }
                else if (_iTypeTemplate == typeof(IBaseStringTemplate)) // get the FormattableString and write to DefaultOutputFile
                {
                    FormattableString fs = (FormattableString)(_entryPointClass.GetMethod(_entryPointMethod.Name, _entryPointMethod.GetParameters().Select(p => p.ParameterType).ToArray()) ?? _entryPointClass.GetMethod(_entryPointMethod.Name))
                        .Invoke(instance, modelArgs.ToArray());
                    _ctx.DefaultOutputFile.Write(fs);
                }
                else if (_iTypeTemplate == null && _entryPointMethod != null) // Main() entrypoint
                {
                    var argTypes = _entryPointMethod.GetParameters().Select(p => p.ParameterType).ToArray();
                    object[] entryPointMethodArgs = new object[argTypes.Length];
                    for (int i = 0; i < argTypes.Length; i++)
                        entryPointMethodArgs[i] = _ctx.DependencyContainer.Resolve(argTypes[i]);
                    if (_entryPointMethod.ReturnType == typeof(FormattableString))
                    {
                        var fs = (FormattableString)_entryPointMethod.Invoke(instance, entryPointMethodArgs);
                        _ctx.DefaultOutputFile.Write(fs);
                    }
                    else if (_entryPointMethod.ReturnType == typeof(FormattableString))
                    {
                        var s = (string)_entryPointMethod.Invoke(instance, entryPointMethodArgs);
                        _ctx.DefaultOutputFile.Write(s);
                    }
                    else
                    {
                        object result = _entryPointMethod.Invoke(instance, entryPointMethodArgs);
                        if (_entryPointMethod.ReturnType == typeof(int) && ((int)result) != 0)
                        {
                            await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"\nExiting with non-zero result code from template ({(int)result}). Nothing saved.");
                            return (int)result;
                        }
                    }
                }
                #endregion
            }
            finally
            {
                if (_executionFolder != null)
                    Directory.SetCurrentDirectory(previousFolder);
            }

            if (_ctx.Errors.Any())
            {
                await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"\nError while building '{providedTemplateName}':");
                foreach (var error in _ctx.Errors)
                    await _logger.WriteLineErrorAsync(ConsoleColor.Red, $"{error}");
                return -1;
            }

            var savedFiles = _ctx.SaveFiles(_outputFolder).SavedFiles;

            if (savedFiles.Count == 0)
            {
                await _logger.WriteLineErrorAsync(ConsoleColor.Yellow, $"No files were generated");
            }
            else if (savedFiles.Count == 1)
            {
                await _logger.WriteLineAsync($"Generated {ConsoleColor.White}{savedFiles.Count}{PREVIOUS_COLOR} file: {ConsoleColor.Yellow}'{_outputFolder.TrimEnd('\\')}\\{_ctx.OutputFilesPaths.Single()}'{PREVIOUS_COLOR}");
            }
            else
            {
                await _logger.WriteLineAsync($"Generated {ConsoleColor.White}{savedFiles.Count}{PREVIOUS_COLOR} files at folder {ConsoleColor.Yellow}'{_outputFolder.TrimEnd('\\')}\\'{PREVIOUS_COLOR}{(VerboseMode ? ":" : "")}");
                if (VerboseMode)
                {
                    foreach (var f in savedFiles)
                        await _logger.WriteLineAsync(ConsoleColor.DarkGray, $"    {f}");
                    await _logger.WriteLineAsync();
                }
            }

            await _logger.WriteLineAsync(ConsoleColor.Green, $"Successfully executed template {ConsoleColor.Yellow}'{providedTemplateName}'{PREVIOUS_COLOR}.");

            return 0;
        }

        void ShowParseResults(BindingContext bindingContext, ParseResult parseResult)
        {
            var helpBuilder = HelpBuilderFactory != null ? HelpBuilderFactory(bindingContext) : new HelpBuilder(parseResult.CommandResult.LocalizationResources);
            var writer = bindingContext.Console.Out.CreateTextWriter();
            helpBuilder.Write(parseResult.CommandResult.Command, writer);
        }


        #region Utils
        private static int GetConsoleWidth()
        {
            int windowWidth;
            try
            {
                windowWidth = System.Console.WindowWidth;
            }
            catch
            {
                windowWidth = int.MaxValue;
            }
            return windowWidth;
        }

        #endregion
    }
}