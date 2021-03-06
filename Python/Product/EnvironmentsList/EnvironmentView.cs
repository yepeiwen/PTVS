// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.EnvironmentsList {
    public sealed class EnvironmentView : DependencyObject {
        public static readonly RoutedCommand OpenInteractiveWindow = new RoutedCommand();
        public static readonly RoutedCommand OpenInteractiveScripts = new RoutedCommand();
        public static readonly RoutedCommand OpenInPowerShell = new RoutedCommand();
        public static readonly RoutedCommand OpenInCommandPrompt = new RoutedCommand();
        public static readonly RoutedCommand MakeGlobalDefault = new RoutedCommand();
        public static readonly RoutedCommand Delete = new RoutedCommand();
        public static readonly RoutedCommand MakeActiveInCurrentProject = new RoutedCommand();

        private const string AddNewEnvironmentViewId = "__AddNewEnvironmentView";
        private const string OnlineHelpViewId = "__OnlineHelpView";
        public const string CondaEnvironmentViewId = "__CondaEnvironmentView";

        public static readonly IEnumerable<InterpreterConfiguration> ExtraItems = new[] {
            new InterpreterConfiguration(OnlineHelpViewId, OnlineHelpViewId),
            new InterpreterConfiguration(AddNewEnvironmentViewId, AddNewEnvironmentViewId),
            new InterpreterConfiguration(CondaEnvironmentViewId, CondaEnvironmentViewId),
        };

        // Names of properties that will be requested from interpreter configurations
        internal const string CompanyKey = "Company";
        internal const string SupportUrlKey = "SupportUrl";

        /// <summary>
        /// Used with <see cref="CommonUtils.FindFile"/> to more efficiently
        /// find interpreter executables.
        /// </summary>
        private static readonly string[] _likelyInterpreterPaths = new[] { "Scripts" };

        private readonly IInterpreterOptionsService _service;
        private readonly IInterpreterRegistryService _registry;

        public IPythonInterpreterFactory Factory { get; }
        public InterpreterConfiguration Configuration { get; }
        public string LocalizedDisplayName { get; }
        public string LocalizedHelpText { get; }
        public string BrokenEnvironmentHelpUrl { get; }
        public bool ExtensionsCreated { get; set; }

        private EnvironmentView(string id, string localizedName, string localizedHelpText) {
            Configuration = new InterpreterConfiguration(id, id);
            Description = LocalizedDisplayName = localizedName;
            LocalizedHelpText = localizedHelpText ?? "";
            Extensions = new ObservableCollection<object>();
        }

        internal EnvironmentView(
            IInterpreterOptionsService service,
            IInterpreterRegistryService registry,
            IPythonInterpreterFactory factory,
            Redirector redirector
        ) {
            if (service == null) {
                throw new ArgumentNullException(nameof(service));
            }
            if (registry == null) {
                throw new ArgumentNullException(nameof(registry));
            }
            if (factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }
            if (factory.Configuration == null) {
                throw new ArgumentException("factory must include a configuration");
            }

            _service = service;
            _registry = registry;
            Factory = factory;
            Configuration = Factory.Configuration;
            LocalizedDisplayName = Configuration.Description;
            IsBroken = !Configuration.IsRunnable();
            BrokenEnvironmentHelpUrl = "https://go.microsoft.com/fwlink/?linkid=863373";

            if (_service.IsConfigurable(Factory.Configuration.Id)) {
                IsConfigurable = true;
            }

            Description = Factory.Configuration.Description;
            IsDefault = (_service != null && _service.DefaultInterpreterId == Configuration.Id);

            PrefixPath = Factory.Configuration.PrefixPath;
            InterpreterPath = Factory.Configuration.InterpreterPath;
            WindowsInterpreterPath = Factory.Configuration.WindowsInterpreterPath;

            Extensions = new ObservableCollection<object>();
            Extensions.Add(new EnvironmentPathsExtensionProvider());
            if (IsConfigurable) {
                Extensions.Add(new ConfigurationExtensionProvider(_service, alwaysCreateNew: false));
            }

            CanBeDefault = Factory.CanBeDefault();
            CanBeDeleted = Factory.CanBeDeleted();

            Company = _registry.GetProperty(Factory.Configuration.Id, CompanyKey) as string ?? "";
            SupportUrl = _registry.GetProperty(Factory.Configuration.Id, SupportUrlKey) as string ?? "";

            LocalizedHelpText = Company;
        }

        public static EnvironmentView CreateAddNewEnvironmentView(IInterpreterOptionsService service) {
            var ev = new EnvironmentView(AddNewEnvironmentViewId, Resources.EnvironmentViewCustomAutomationName, null);
            ev.Extensions = new ObservableCollection<object>();
            ev.Extensions.Add(new ConfigurationExtensionProvider(service, alwaysCreateNew: true));
            return ev;
        }

        public static EnvironmentView CreateOnlineHelpEnvironmentView() {
            return new EnvironmentView(OnlineHelpViewId, Resources.EnvironmentViewOnlineHelpLabel, null);
        }

        public static EnvironmentView CreateCondaEnvironmentView(IInterpreterOptionsService service, IInterpreterRegistryService interpreters) {
            var ev = new EnvironmentView(CondaEnvironmentViewId, Resources.EnvironmentViewCreateNewCondaEnvironmentAutomationName, null);
            ev.Extensions = new ObservableCollection<object>();
            ev.Extensions.Add(new CondaExtensionProvider(service, interpreters));
            return ev;
        }

        public static EnvironmentView CreateMissingEnvironmentView(string id, string description) {
            return new EnvironmentView(id, description + Strings.MissingSuffix, null);
        }

        public static bool IsAddNewEnvironmentView(string id) => AddNewEnvironmentViewId.Equals(id);
        public static bool IsCondaEnvironmentView(string id) => CondaEnvironmentViewId.Equals(id);
        public static bool IsOnlineHelpView(string id) => OnlineHelpViewId.Equals(id);

        public static bool IsAddNewEnvironmentView(EnvironmentView view) => AddNewEnvironmentViewId.Equals(view?.Configuration.Id);
        public static bool IsCondaEnvironmentView(EnvironmentView view) => CondaEnvironmentViewId.Equals(view?.Configuration.Id);
        public static bool IsOnlineHelpView(EnvironmentView view) => OnlineHelpViewId.Equals(view?.Configuration.Id);

        public ObservableCollection<object> Extensions { get; private set; }

        #region Read-only State Dependency Properties

        private static readonly DependencyPropertyKey IsConfigurablePropertyKey = DependencyProperty.RegisterReadOnly("IsConfigurable", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(false));
        private static readonly DependencyPropertyKey CanBeDeletedPropertyKey = DependencyProperty.RegisterReadOnly("CanBeDeleted", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(false));
        private static readonly DependencyPropertyKey CanBeDefaultPropertyKey = DependencyProperty.RegisterReadOnly("CanBeDefault", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(true));
        private static readonly DependencyPropertyKey IsDefaultPropertyKey = DependencyProperty.RegisterReadOnly("IsDefault", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(false));
        private static readonly DependencyPropertyKey IsBrokenPropertyKey = DependencyProperty.RegisterReadOnly("IsBroken", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty IsConfigurableProperty = IsConfigurablePropertyKey.DependencyProperty;
        public static readonly DependencyProperty CanBeDeletedProperty = CanBeDeletedPropertyKey.DependencyProperty;
        public static readonly DependencyProperty CanBeDefaultProperty = CanBeDefaultPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsDefaultProperty = IsDefaultPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsBrokenProperty = IsBrokenPropertyKey.DependencyProperty;

        public bool IsConfigurable {
            get { return Factory == null ? false : (bool)GetValue(IsConfigurableProperty); }
            set { if (Factory != null) { SetValue(IsConfigurablePropertyKey, value); } }
        }

        public bool CanBeDeleted {
            get { return Factory == null ? false : (bool)GetValue(CanBeDeletedProperty); }
            set { if (Factory != null) { SetValue(CanBeDeletedPropertyKey, value); } }
        }

        public bool CanBeDefault {
            get { return Factory == null ? false : (bool)GetValue(CanBeDefaultProperty); }
            set { if (Factory != null) { SetValue(CanBeDefaultPropertyKey, value); } }
        }

        public bool IsDefault {
            get { return Factory == null ? false : (bool)GetValue(IsDefaultProperty); }
            internal set { if (Factory != null) { SetValue(IsDefaultPropertyKey, value); } }
        }

        public bool IsBroken {
            get { return Factory == null ? false : (bool)GetValue(IsBrokenProperty); }
            internal set { if (Factory != null) { SetValue(IsBrokenPropertyKey, value); } }
        }

        #endregion

        #region Configuration Dependency Properties

        private static readonly DependencyPropertyKey DescriptionPropertyKey = DependencyProperty.RegisterReadOnly("Description", typeof(string), typeof(EnvironmentView), new PropertyMetadata(""));
        private static readonly DependencyPropertyKey PrefixPathPropertyKey = DependencyProperty.RegisterReadOnly("PrefixPath", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey InterpreterPathPropertyKey = DependencyProperty.RegisterReadOnly("InterpreterPath", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey WindowsInterpreterPathPropertyKey = DependencyProperty.RegisterReadOnly("WindowsInterpreterPath", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey PathEnvironmentVariablePropertyKey = DependencyProperty.RegisterReadOnly("PathEnvironmentVariable", typeof(string), typeof(EnvironmentView), new PropertyMetadata());

        public static readonly DependencyProperty DescriptionProperty = DescriptionPropertyKey.DependencyProperty;
        public static readonly DependencyProperty PrefixPathProperty = PrefixPathPropertyKey.DependencyProperty;
        public static readonly DependencyProperty InterpreterPathProperty = InterpreterPathPropertyKey.DependencyProperty;
        public static readonly DependencyProperty WindowsInterpreterPathProperty = WindowsInterpreterPathPropertyKey.DependencyProperty;
        public static readonly DependencyProperty PathEnvironmentVariableProperty = PathEnvironmentVariablePropertyKey.DependencyProperty;

        public string Description {
            get { return (string)GetValue(DescriptionProperty) ?? ""; }
            set { SetValue(DescriptionPropertyKey, value ?? ""); }
        }

        public string PrefixPath {
            get { return Factory == null ? string.Empty : (string)GetValue(PrefixPathProperty); }
            set { if (Factory != null) { SetValue(PrefixPathPropertyKey, value); } }
        }

        public string InterpreterPath {
            get { return Factory == null ? string.Empty : (string)GetValue(InterpreterPathProperty); }
            set { if (Factory != null) { SetValue(InterpreterPathPropertyKey, value); } }
        }

        public string WindowsInterpreterPath {
            get { return Factory == null ? string.Empty : (string)GetValue(WindowsInterpreterPathProperty); }
            set { if (Factory != null) { SetValue(WindowsInterpreterPathPropertyKey, value); } }
        }

        public string PathEnvironmentVariable {
            get { return Factory == null ? string.Empty : (string)GetValue(PathEnvironmentVariableProperty); }
            set { if (Factory != null) { SetValue(PathEnvironmentVariablePropertyKey, value); } }
        }

        #endregion

        #region Extra Information Dependency Properties

        private static readonly DependencyPropertyKey CompanyPropertyKey = DependencyProperty.RegisterReadOnly("Company", typeof(string), typeof(EnvironmentView), new PropertyMetadata(""));
        private static readonly DependencyPropertyKey SupportUrlPropertyKey = DependencyProperty.RegisterReadOnly("SupportUrl", typeof(string), typeof(EnvironmentView), new PropertyMetadata(""));

        public static readonly DependencyProperty CompanyProperty = CompanyPropertyKey.DependencyProperty;
        public static readonly DependencyProperty SupportUrlProperty = SupportUrlPropertyKey.DependencyProperty;

        public string Company {
            get { return (string)GetValue(CompanyProperty) ?? ""; }
            set { SetValue(CompanyPropertyKey, value ?? ""); }
        }

        public string SupportUrl {
            get { return (string)GetValue(SupportUrlProperty) ?? ""; }
            set { SetValue(SupportUrlPropertyKey, value ?? ""); }
        }

        #endregion

        public static readonly DependencyProperty IsIPythonModeEnabledProperty = DependencyProperty.Register("IsIPythonModeEnabled", typeof(bool?), typeof(EnvironmentView), new FrameworkPropertyMetadata(null, OnIsIPythonModeEnabledChanged));

        public bool? IsIPythonModeEnabled {
            get { return (bool?)GetValue(IsIPythonModeEnabledProperty); }
            set { SetValue(IsIPythonModeEnabledProperty, value); }
        }

        public Func<EnvironmentView, bool> IPythonModeEnabledGetter { get; set; }
        public Action<EnvironmentView, bool> IPythonModeEnabledSetter { get; set; }

        private static void OnIsIPythonModeEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var view = (EnvironmentView)d;
            view.IPythonModeEnabledSetter?.Invoke(view, (bool)e.NewValue);
        }
    }

    sealed class EnvironmentViewTemplateSelector : DataTemplateSelector {
        public DataTemplate Environment { get; set; }

        public DataTemplate CondaEnvironment { get; set; }

        public DataTemplate AddNewEnvironment { get; set; }

        public DataTemplate OnlineHelp { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            var ev = item as EnvironmentView;
            if (ev == null) {
                return base.SelectTemplate(item, container);
            }

            if (EnvironmentView.IsAddNewEnvironmentView(ev) && AddNewEnvironment != null) {
                return AddNewEnvironment;
            }

            if (EnvironmentView.IsCondaEnvironmentView(ev) && CondaEnvironment != null) {
                return CondaEnvironment;
            }

            if (EnvironmentView.IsOnlineHelpView(ev) && OnlineHelp != null) {
                return OnlineHelp;
            }

            if (Environment != null) {
                return Environment;
            }

            return base.SelectTemplate(item, container);
        }
    }

    sealed class EnvironmentViewItemContainerSelector : StyleSelector {
        public Style Environment { get; set; }
        public Style OnlineHelp { get; set; }

        public override Style SelectStyle(object item, DependencyObject container) {
            return SelectStyle(item as EnvironmentView)
                ?? container.GetValue(ItemsControl.ItemContainerStyleProperty) as Style
                ?? base.SelectStyle(item, container);
        }

        private Style SelectStyle(EnvironmentView ev) {
            if (ev == null) {
                return null;
            }

            if (EnvironmentView.IsOnlineHelpView(ev) && OnlineHelp != null) {
                return OnlineHelp;
            }

            if (Environment != null) {
                return Environment;
            }

            return null;
        }

    }
}
