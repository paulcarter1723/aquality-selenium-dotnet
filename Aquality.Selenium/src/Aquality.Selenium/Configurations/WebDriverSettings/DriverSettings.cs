﻿using Aquality.Selenium.Browsers;
using Aquality.Selenium.Utilities;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WebDriverManager.Helpers;

namespace Aquality.Selenium.Configurations.WebDriverSettings
{
    /// <summary>
    /// Abstract representation of web driver settings.
    /// </summary>
    public abstract class DriverSettings : IDriverSettings
    {
        /// <summary>
        /// Instantiates class using JSON file with general settings.
        /// </summary>
        /// <param name="settingsFile">JSON settings file.</param>
        public DriverSettings(JsonFile settingsFile)
        {
            SettingsFile = settingsFile;
        }

        private string DriverSettingsPath
        {
            get
            {
                return $".driverSettings.{BrowserName.ToString().ToLowerInvariant()}";
            }
        }

        protected IDictionary<string, object> BrowserCapabilities
        {
            get
            {
                return SettingsFile.GetObject<IDictionary<string, object>>($"{DriverSettingsPath}.capabilities");
            }
        }

        protected IDictionary<string, object> BrowserOptions
        {
            get
            {
                return SettingsFile.GetObject<IDictionary<string, object>>($"{DriverSettingsPath}.options");
            }
        }

        protected IList<string> BrowserStartArguments
        {
            get
            {
                return SettingsFile.GetObject<IList<string>>($"{DriverSettingsPath}.startArguments");
            }
        }

        protected JsonFile SettingsFile { get; }

        protected abstract BrowserName BrowserName { get; }

        protected virtual IDictionary<string, Action<DriverOptions, object>> KnownCapabilitySetters => new Dictionary<string, Action<DriverOptions, object>>();

        public abstract string DownloadDirCapabilityKey { get; }

        public abstract DriverOptions DriverOptions { get; }

        public string WebDriverVersion
        {
            get
            {
                var jsonPath = $"{DriverSettingsPath}.webDriverVersion";
                return SettingsFile.IsValuePresent(jsonPath)
                ? SettingsFile.GetObject<string>(jsonPath)
                : "Latest";
            }
        }

        public Architecture SystemArchitecture
        {
            get
            {
                var jsonPath = $"{DriverSettingsPath}.systemArchitecture";
                return SettingsFile.IsValuePresent(jsonPath)
                ? SettingsFile.GetObject<string>(jsonPath).ToEnum<Architecture>()
                : Architecture.Auto;
            }
        }

        public virtual string DownloadDir
        {
            get
            {
                if (BrowserOptions.ContainsKey(DownloadDirCapabilityKey))
                {
                    var pathInConfiguration = BrowserOptions[DownloadDirCapabilityKey].ToString();
                    return pathInConfiguration.Contains(".") ? Path.GetFullPath(pathInConfiguration) : pathInConfiguration;
                }

                throw new InvalidDataException($"failed to find {DownloadDirCapabilityKey} option in settings profile for {BrowserName}");
            }
        }

        protected void SetCapabilities(DriverOptions options, Action<string, object> addCapabilityMethod = null)
        {
            foreach(var capability in BrowserCapabilities)
            {
                try
                {
                    var defaultAddCapabilityMethod = addCapabilityMethod ?? options.AddAdditionalCapability;
                    defaultAddCapabilityMethod(capability.Key, capability.Value);
                } 
                catch(ArgumentException exception)
                {
                    if(exception.Message.StartsWith("There is already an option"))
                    {
                        SetKnownProperty(options, capability, exception);
                    }
                    else
                    {
                        throw exception;
                    }
                }
            }
        }

        private void SetKnownProperty(DriverOptions options, KeyValuePair<string, object> capability, ArgumentException exception)
        {
            if (KnownCapabilitySetters.ContainsKey(capability.Key))
            {
                KnownCapabilitySetters[capability.Key](options, capability.Value);
            }
            else
            {
                SetOptionByPropertyName(options, capability, exception);
            }            
        }

        protected void SetOptionsByPropertyNames(DriverOptions options)
        {
            foreach (var option in BrowserOptions)
            {
                SetOptionByPropertyName(options, option, new NotSupportedException($"Property for option {option.Key} is not supported"));
            }
        }

        private void SetOptionByPropertyName(DriverOptions options, KeyValuePair<string, object> option, Exception exception)
        {
            var optionProperty = options
                            .GetType()
                            .GetProperties()
                            .FirstOrDefault(property => IsPropertyNameMatchOption(property.Name, option.Key) && property.CanWrite)
                            ?? throw exception;
            optionProperty.SetValue(options, option.Value);
        }

        private bool IsPropertyNameMatchOption(string propertyName, string optionKey)
        {
            return propertyName.Equals(optionKey, StringComparison.InvariantCultureIgnoreCase)
                || optionKey.ToLowerInvariant().Contains(propertyName.ToLowerInvariant());
        }
    }
}
