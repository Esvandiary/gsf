﻿//******************************************************************************************************
//  IgnoreServicesSecurityModule.cs - Gbtc
//
//  Copyright © 2012, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/eclipse-1.0.php
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  03/31/2010 - Pinal C. Patel
//       Generated original version of source code.
//
//******************************************************************************************************

using System.Web;

namespace GSF.Web
{
    /// <summary>
    /// Represents an <see cref="IHttpModule">HTTP module</see> that can be used to enable site-wide role-based security except for WCF services.
    /// </summary>
    public class IgnoreServicesSecurityModule : SecurityModule
    {
        /// <summary>
        /// Determines if access to the requested <paramref name="resource"/> is to be secured.
        /// </summary>
        /// <param name="resource">Name of the resource being requested.</param>
        /// <returns>True if access to the resource is to be secured, otherwise False.</returns>
        protected override bool IsAccessSecured(string resource)
        {
            if (resource.Contains(".svc"))
                // Don't secure WCF services.
                return false;
            else
                // Fallback to the base class for everything else.
                return base.IsAccessSecured(resource);
        }
    }
}
