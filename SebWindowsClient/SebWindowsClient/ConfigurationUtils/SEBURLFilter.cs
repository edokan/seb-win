﻿using System;
using System.Text;
using System.Text.RegularExpressions;
using ListObj = System.Collections.Generic.List<object>;
using DictObj = System.Collections.Generic.Dictionary<string, object>;

namespace SebWindowsClient.ConfigurationUtils
{
    class SEBURLFilter
    {
        public bool enableURLFilter;
        public bool enableContentFilter;
        public ListObj permittedList = new ListObj();
        public ListObj prohibitedList = new ListObj();

        // Updates filter rule arrays with current settings (UserDefaults)
        public void UpdateFilterRules()
        {
            if (prohibitedList.Count != 0)
            {
                prohibitedList.Clear();
            }

            if (permittedList.Count != 0)
            {
                permittedList.Clear();
            }


            enableURLFilter = (bool)SEBSettings.settingsCurrent[SEBSettings.KeyURLFilterEnable];
            enableContentFilter = (bool)SEBSettings.settingsCurrent[SEBSettings.KeyURLFilterEnableContentFilter];

            ListObj URLFilterRules = (ListObj)SEBSettings.settingsCurrent[SEBSettings.KeyURLFilterRules];

            foreach (DictObj URLFilterRule in URLFilterRules) {

                if ((bool)URLFilterRule["active"] == true)
                {

                    string expressionString = (string)URLFilterRule["expression"];
                    if (String.IsNullOrEmpty(expressionString))
                    {
                        Object expression;

                        bool regex = (bool)URLFilterRule["regex"];
                        try
                        {
                            if (regex)
                            {
                                expression = new Regex(expressionString, RegexOptions.IgnoreCase);
                            }
                            else
                            {
                                expression = new SEBURLFilterRegexExpression(expressionString);
                            }
                        }
                        catch (Exception)
                        {
                            prohibitedList.Clear();
                            permittedList.Clear();
                            throw;
                        }

                        int action = (int)URLFilterRule["action"];
                        switch (action) {

                            case (int)URLFilterRuleActions.block:

                                prohibitedList.Add(expression);
                                break;


                            case (int)URLFilterRuleActions.allow:

                                permittedList.Add(expression);
                                break;
                        }
                    }
                }
            }

            // Check if Start URL gets allowed by current filter rules and if not add a rule for the Start URL
            string startURLString = (string)SEBSettings.settingsCurrent[SEBSettings.KeyStartURL]; ;

            if (!Uri.TryCreate(startURLString, UriKind.RelativeOrAbsolute, out Uri startURL))
            {
                if (true /*testURLAllowed(startURL) != URLFilterRuleActions.allow*/)
                {
                    Regex expression;
                    // If Start URL is not allowed: Create one using the full Start URL
                    try
                    {
                        expression = SEBURLFilterRegexExpression.RegexForHostFilterString(startURLString);
                    }
                    catch (Exception)
                    {
                        prohibitedList.Clear();
                        permittedList.Clear();
                        return;
                    }

                    // Add this Start URL filter expression to the permitted filter list
                    permittedList.Add(expression);

                }
            }
            // Convert these rules and add them to the XULRunner seb keys
            CreateSebRuleLists();
        }


        // Convert these rules and add them to the XULRunner seb keys
        public void CreateSebRuleLists()
        {
            // Set prohibited rules
            SEBSettings.settingsCurrent[SEBSettings.KeyUrlFilterBlacklist] = SebRuleStringForSEBURLFilterRuleList(prohibitedList);

            // Set permitted rules
            SEBSettings.settingsCurrent[SEBSettings.KeyUrlFilterWhitelist] = SebRuleStringForSEBURLFilterRuleList(permittedList);

            // All rules are regex
            SEBSettings.settingsCurrent[SEBSettings.KeyUrlFilterRulesAsRegex] = true;

            // Set if content filter is enabled
            SEBSettings.settingsCurrent[SEBSettings.KeyUrlFilterTrustedContent] = SEBSettings.settingsCurrent[SEBSettings.KeyURLFilterEnableContentFilter];
        }


        public string SebRuleStringForSEBURLFilterRuleList(ListObj filterRuleList)
        {
            if (filterRuleList.Count == 0)
            {
                // No rules defined
                return "";
            }

            StringBuilder sebRuleString = new StringBuilder();
            foreach (object expression in filterRuleList)
            {
                if (expression != null)
                {
                    if (sebRuleString.Length == 0)
                    {
                        sebRuleString.Append(expression.ToString());
                    }
                    else
                    {
                        sebRuleString.AppendFormat(";{0}", expression.ToString());
                    }
                }
            }

            return sebRuleString.ToString();
        }


    // Filter URL and return if it is allowed or blocked
    /*
public URLFilterRuleActions TestURLAllowed(Uri URLToFilter)
{
    string URLToFilterString = URLToFilter.ToString();
    // By default URLs are blocked
    bool allowURL = false;
    bool blockURL = false;
    
    /// Apply current filter rules (expressions/actions) to URL
    /// Apply prohibited filter expressions
    
    foreach (object expression in prohibitedList) {

        if (expression.GetType().Equals(typeof(Regex))) {
            if (Regex.IsMatch(URLToFilterString, expression.ToString())) {
                blockURL = true;
                break;
            }
        }
        
        if ([expression isKindOfClass:[SEBURLFilterRegexExpression class]]) {
            if ([self URL:(NSURL*)URLToFilter matchesFilterExpression:expression]) {
                blockURL = YES;
                break;
            }
        }
    }
    if (blockURL == true) {
        return URLFilterActionBlock;
    }
    
    /// Apply permitted filter expressions
    
    for (expression in self.permittedList) {
        
        if ([expression isKindOfClass:[NSRegularExpression class]]) {
            if ([self regexFilterExpression:expression hasMatchesInString:URLToFilterString]) {
                allowURL = YES;
                break;
            }
        }
        
        if ([expression isKindOfClass:[SEBURLFilterRegexExpression class]]) {
            if ([self URL:(NSURL*)URLToFilter matchesFilterExpression:expression]) {
                allowURL = YES;
                break;
            }
        }
    }
    // Return URLFilterActionAllow if URL is allowed or
    // URLFilterActionUnknown if it's unknown (= it will anyways be blocked)
    return allowURL? URLFilterActionAllow : URLFilterActionUnknown;
}
*/

    // Method comparing all components of a passed URL with the filter expression
// and returning YES (= allow or block) if it matches
public bool URLMatchesFilterExpression(Uri URLToFilter, SEBURLFilterRegexExpression filterExpression)
{
    Regex filterComponent;

    // If a scheme is indicated in the filter expression, it has to match
    filterComponent = filterExpression.scheme;
    if (filterComponent != null &&
        !Regex.IsMatch(URLToFilter.Scheme, filterComponent.ToString(), RegexOptions.IgnoreCase)) {
        // Scheme of the URL to filter doesn't match the one from the filter expression: Exit with matching = NO
        return false;
    }

            string userInfo = URLToFilter.UserInfo;
    filterComponent = filterExpression.user;
            if (filterComponent != null &&
        !Regex.IsMatch(SEBURLFilterExpression.User(userInfo), filterComponent.ToString(), RegexOptions.IgnoreCase)) {
        return false;
    }

    filterComponent = filterExpression.password;
            if (filterComponent != null &&
        !Regex.IsMatch(SEBURLFilterExpression.Password(userInfo), filterComponent.ToString(), RegexOptions.IgnoreCase))
            {
                return false;
    }

    filterComponent = filterExpression.host;
            if (filterComponent != null &&
        !Regex.IsMatch(URLToFilter.Host, filterComponent.ToString(), RegexOptions.IgnoreCase))
            {
                return false;
    }
    
    if (filterExpression.port && URLToFilter.Port != -1 &&
        URLToFilter.port.intValue != filterExpression.port.intValue) {
        return false;
    }

    filterComponent = filterExpression.path;
            if (filterComponent != null &&
                ![self regexFilterExpression:filterComponent hasMatchesInString:[URLToFilter.path stringByTrimmingCharactersInSet:[NSCharacterSet characterSetWithCharactersInString:@"/"]]]) {
        return false;
    }

    filterComponent = filterExpression.query;
            if (filterComponent != null &&
                ![self regexFilterExpression:filterComponent hasMatchesInString:URLToFilter.query]) {
        return false;
    }

    filterComponent = filterExpression.fragment;
            if (filterComponent != null &&
                ![self regexFilterExpression:filterComponent hasMatchesInString:URLToFilter.fragment]) {
        return false;
    }
    
    // URL matches the filter expression
    return true;
}


    }

}
