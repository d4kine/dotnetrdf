/*

Copyright Robert Vesse 2009-10
rvesse@vdesign-studios.com

------------------------------------------------------------------------

This file is part of dotNetRDF.

dotNetRDF is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

dotNetRDF is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with dotNetRDF.  If not, see <http://www.gnu.org/licenses/>.

------------------------------------------------------------------------

dotNetRDF may alternatively be used under the LGPL or MIT License

http://www.gnu.org/licenses/lgpl.html
http://www.opensource.org/licenses/mit-license.php

If these licenses are not suitable for your intended use please contact
us at the above stated email address to discuss alternative
terms.

*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VDS.RDF.Configuration;
using VDS.RDF.Parsing;
using VDS.RDF.Writing;

namespace VDS.RDF.Storage
{
    /// <summary>
    /// Class for connecting to an AllegroGraph Store
    /// </summary>
    /// <remarks>
    /// <para>
    /// Connection to AllegroGraph is based on their new HTTP Protocol which is an extension of the <a href="http://www.openrdf.org/doc/sesame2/system/ch08.html">Sesame 2.0 HTTP Protocol</a>.  The specification for the AllegroGraph protocol can be found <a href="http://www.franz.com/agraph/support/documentation/current/new-http-server.html">here</a>
    /// </para>
    /// <para>
    /// If you wish to use a Store which is part of the Root Catalog on an AllegroGraph 4.x and higher server you can either use the constructor overloads that omit the <strong>catalogID</strong> parameter or pass in null as the value for that parameter
    /// </para>
    /// </remarks>
    public class AllegroGraphConnector
        : BaseSesameHttpProtocolConnector, IAsyncStorageServer, IConfigurationSerializable
#if !NO_SYNC_HTTP
        , IStorageServer, IMultiStoreGenericIOManager
#endif
    {
        private String _agraphBase;
        private String _catalog;
        private bool _ready = false;
         
        public AllegroGraphConnector(String baseUri, String catalogID, String storeID)
            : this(baseUri, storeID, catalogID, (String)null, (String)null) { }

        /// <summary>
        /// Creates a new Connection to an AllegroGraph store in the Root Catalog (AllegroGraph 4.x and higher)
        /// </summary>
        /// <param name="baseUri">Base Uri for the Store</param>
        /// <param name="storeID">Store ID</param>
        public AllegroGraphConnector(String baseUri, String storeID)
            : this(baseUri, null, storeID) { }

        /// <summary>
        /// Creates a new Connection to an AllegroGraph store
        /// </summary>
        /// <param name="baseUri">Base Uri for the Store</param>
        /// <param name="catalogID">Catalog ID</param>
        /// <param name="storeID">Store ID</param>
        /// <param name="username">Username for connecting to the Store</param>
        /// <param name="password">Password for connecting to the Store</param>
        public AllegroGraphConnector(String baseUri, String catalogID, String storeID, String username, String password)
            : base(baseUri, storeID, username, password)
        {
            this._baseUri = baseUri;
            if (!this._baseUri.EndsWith("/")) this._baseUri += "/";
            this._agraphBase = String.Copy(this._baseUri);
            if (catalogID != null)
            {
                this._baseUri += "catalogs/" + catalogID + "/";
            }
            this._store = storeID;
            this._catalog = catalogID;

#if !NO_SYNC_HTTP
            this.CreateStore(storeID);
#else
            this.CreateStore(storeID, (sender, args, state) =>
                {
                    if (args.WasSuccessful) this._ready = true;
                }, null);
#endif
        }

        /// <summary>
        /// Creates a new Connection to an AllegroGraph store in the Root Catalog (AllegroGraph 4.x and higher)
        /// </summary>
        /// <param name="baseUri">Base Uri for the Store</param>
        /// <param name="storeID">Store ID</param>
        /// <param name="username">Username for connecting to the Store</param>
        /// <param name="password">Password for connecting to the Store</param>
        public AllegroGraphConnector(String baseUri, String storeID, String username, String password)
            : this(baseUri, null, storeID, username, password) { }

#if !NO_PROXY

        /// <summary>
        /// Creates a new Connection to an AllegroGraph store
        /// </summary>
        /// <param name="baseUri">Base Uri for the Store</param>
        /// <param name="catalogID">Catalog ID</param>
        /// <param name="storeID">Store ID</param>
        /// <param name="proxy">Proxy Server</param>
        public AllegroGraphConnector(String baseUri, String catalogID, String storeID, WebProxy proxy)
            : this(baseUri, catalogID, storeID, null, null, proxy) { }

        /// <summary>
        /// Creates a new Connection to an AllegroGraph store in the Root Catalog (AllegroGraph 4.x and higher)
        /// </summary>
        /// <param name="baseUri">Base Uri for the Store</param>
        /// <param name="storeID">Store ID</param>
        /// <param name="proxy">Proxy Server</param>
        public AllegroGraphConnector(String baseUri, String storeID, WebProxy proxy)
            : this(baseUri, null, storeID, proxy) { }

        /// <summary>
        /// Creates a new Connection to an AllegroGraph store
        /// </summary>
        /// <param name="baseUri">Base Uri for the Store</param>
        /// <param name="catalogID">Catalog ID</param>
        /// <param name="storeID">Store ID</param>
        /// <param name="username">Username for connecting to the Store</param>
        /// <param name="password">Password for connecting to the Store</param>
        /// <param name="proxy">Proxy Server</param>
        public AllegroGraphConnector(String baseUri, String catalogID, String storeID, String username, String password, WebProxy proxy)
            : this(baseUri, storeID, username, password, proxy)
        {
            this.Proxy = proxy;
        }

        /// <summary>
        /// Creates a new Connection to an AllegroGraph store in the Root Catalog (AllegroGraph 4.x and higher)
        /// </summary>
        /// <param name="baseUri">Base Uri for the Store</param>
        /// <param name="storeID">Store ID</param>
        /// <param name="username">Username for connecting to the Store</param>
        /// <param name="password">Password for connecting to the Store</param>
        /// <param name="proxy">Proxy Server</param>
        public AllegroGraphConnector(String baseUri, String storeID, String username, String password, WebProxy proxy)
            : this(baseUri, null, storeID, username, password, proxy) { }

#endif

        /// <summary>
        /// Gets the Catalog under which the repository you are connected to is located
        /// </summary>
        [Description("The Catalog under which the repository is located.  If using the Root Catalog on AllegroGrah 4+ <ROOT> will be displayed.")]
        public String Catalog
        {
            get
            {
                return (this._catalog != null ? this._catalog : "<ROOT>");
            }
        }

        /// <summary>
        /// Gets whether the Store is ready
        /// </summary>
        /// <remarks>
        /// Usually just instantiating the class should make the store ready assuming the parameters given point to a valid AllegroGraph instance but in the case of an async only environment (Silverlight/Windows Phone 7) this will not be the case and it will be necessary to wait for the async store creation to complete
        /// </remarks>
        public override bool IsReady
        {
            get
            {
                return this._ready;
            }
        }

#if !NO_SYNC_HTTP

        /// <summary>
        /// Creates a new Store (if it doesn't exist) and switches the connector to use that Store
        /// </summary>
        /// <param name="storeID">Store ID</param>
        public override void CreateStore(String storeID)
        {
            HttpWebRequest request = null;
            HttpWebResponse response = null;
            try
            {
                Dictionary<String, String> createParams = new Dictionary<string, string>();
                createParams.Add("override", "false");
                request = this.CreateRequest("repositories/" + storeID, "*/*", "PUT", createParams);

#if DEBUG
                if (Options.HttpDebugging)
                {
                    Tools.HttpDebugRequest(request);
                }
#endif

                using (response = (HttpWebResponse)request.GetResponse())
                {
#if DEBUG
                    if (Options.HttpDebugging)
                    {
                        Tools.HttpDebugResponse(response);
                    }
#endif
                    response.Close();
                }
                this._ready = true;
            }
            catch (WebException webEx)
            {
                if (webEx.Response != null)
                {
#if DEBUG
                    if (Options.HttpDebugging)
                    {
                        if (webEx.Response != null) Tools.HttpDebugResponse((HttpWebResponse)webEx.Response);
                    }
#endif
                    //Got a Response so we can analyse the Response Code
                    response = (HttpWebResponse)webEx.Response;
                    int code = (int)response.StatusCode;
                    if (code == 400)
                    {
                        //OK - Just means the Store already exists
                        this._ready = true;
                    }
                    else
                    {
                        throw;
                    }
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                this._store = storeID;
            }
        }

        /// <summary>
        /// Requests that AllegroGraph indexes the Store
        /// </summary>
        /// <param name="combineIndices">Whether existing Indices should be combined with the newly generated ones</param>
        /// <remarks>
        /// Setting <paramref name="combineIndices"/> causes AllegroGraph to merge the new indices with existing indices which results in faster queries but may take significant extra time for the indexing to be done depending on the size of the Store.
        /// </remarks>
        public void IndexStore(bool combineIndices)
        {
            try
            {
                Dictionary<String,String> requestParams = new Dictionary<string,string>();
                if (combineIndices) 
                {
                    requestParams.Add("all","true");
                } 
                else 
                {
                    requestParams.Add("all","false");
                }
                HttpWebRequest request = this.CreateRequest("repositories/" + this._store + "/indexing", "*/*", "POST", requestParams);

#if DEBUG
                if (Options.HttpDebugging)
                {
                    Tools.HttpDebugRequest(request);
                }
#endif

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
#if DEBUG
                    if (Options.HttpDebugging)
                    {
                        Tools.HttpDebugResponse(response);
                    }
#endif
                    response.Close();
                }
            }
            catch (WebException webEx)
            {
#if DEBUG
                if (Options.HttpDebugging)
                {
                    if (webEx.Response != null) Tools.HttpDebugResponse((HttpWebResponse)webEx.Response);
                }
#endif
                throw new RdfStorageException("A HTTP Error occurred while attempting to index a Store", webEx);
            }
        }

        /// <summary>
        /// Requests that AllegroGraph deletes a Store
        /// </summary>
        /// <param name="storeID">Store ID</param>
        public override void DeleteStore(String storeID)
        {
            try
            {
                HttpWebRequest request = this.CreateRequest("repositories/" + this._store, "*/*", "DELETE", new Dictionary<string, string>());

#if DEBUG
                if (Options.HttpDebugging)
                {
                    Tools.HttpDebugRequest(request);
                }
#endif

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
#if DEBUG
                    if (Options.HttpDebugging)
                    {
                        Tools.HttpDebugResponse(response);
                    }
#endif
                    response.Close();
                }
            }
            catch (WebException webEx)
            {
#if DEBUG
                if (Options.HttpDebugging)
                {
                    if (webEx.Response != null) Tools.HttpDebugResponse((HttpWebResponse)webEx.Response);
                }
#endif
                throw new RdfStorageException("A HTTP Error occurred while attempting to delete a Store", webEx);
            }
        }

        public override IEnumerable<String> ListStores()
        {
            HttpWebRequest request = this.CreateRequest("repositories", "application/json", "GET", new Dictionary<string, string>());
            String data;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    data = reader.ReadToEnd();
                    reader.Close();
                }
                response.Close();
            }

            JArray json = JArray.Parse(data);
            List<String> stores = new List<string>();
            foreach (JToken token in json.Children())
            {
                JValue id = token["id"] as JValue;
                if (id != null)
                {
                    stores.Add(id.Value.ToString());
                }
            }
            return stores;
        }

        /// <summary>
        /// Gets a Store within the current catalog
        /// </summary>
        /// <param name="storeID">Store ID</param>
        /// <returns></returns>
        /// <remarks>
        /// AllegroGraph groups stores by catalogue, you may only use this method to obtain stores within your current catalogue
        /// </remarks>
        public override IStorageProvider GetStore(String storeID)
        {
            //Allowed to return self when given ID matches own ID
            if (this._store.Equals(storeID)) return this;

            //Otherwise return a new instance
            return new AllegroGraphConnector(this._agraphBase, this._catalog, storeID, this._username, this._pwd, this.Proxy);
        }

#endif
        public override void ListStores(AsyncStorageCallback callback, object state)
        {
            try
            {
                HttpWebRequest request = this.CreateRequest("repositories", "application/json", "GET", new Dictionary<string, string>());
                request.BeginGetResponse(r =>
                    {
                        try
                        {
                            HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(r);
                            String data;
                            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                            {
                                data = reader.ReadToEnd();
                                reader.Close();
                            }

                            JArray json = JArray.Parse(data);
                            List<String> stores = new List<string>();
                            foreach (JToken token in json.Children())
                            {
                                JValue id = token["id"] as JValue;
                                if (id != null)
                                {
                                    stores.Add(id.Value.ToString());
                                }
                            }
                            callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.ListStores, stores), state);
                        }
                        catch (WebException webEx)
                        {
    #if DEBUG
                            if (Options.HttpDebugging)
                            {
                                if (webEx.Response != null) Tools.HttpDebugResponse((HttpWebResponse)webEx.Response);
                            }
    #endif
                            callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.ListStores, new RdfStorageException("A HTTP Error occurred while attempting to list Stores, see inner exception for details", webEx)), state);
                        }
                        catch (Exception ex)
                        {
                            callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.ListStores,  new RdfStorageException("An unexpected error occurred while attempting to list Stores, see inner exception for details", ex)), state);
                        }
                    }, state);
            }
            catch (WebException webEx)
            {
#if DEBUG
                if (Options.HttpDebugging)
                {
                    if (webEx.Response != null) Tools.HttpDebugResponse((HttpWebResponse)webEx.Response);
                }
#endif
                callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.ListStores, new RdfStorageException("A HTTP Error occurred while attempting to list Stores, see inner exception for details", webEx)), state);
            }
            catch (Exception ex)
            {
                callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.ListStores, new RdfStorageException("An unexpected error occurred while attempting to list Stores, see inner exception for details", ex)), state);
            }
        }

        public override void CreateStore(string storeID, AsyncStorageCallback callback, object state)
        {
            try
            {
                Dictionary<String, String> createParams = new Dictionary<string, string>();
                createParams.Add("override", "false");
                HttpWebRequest request = this.CreateRequest("repositories/" + storeID, "*/*", "PUT", createParams);

#if DEBUG
                if (Options.HttpDebugging)
                {
                    Tools.HttpDebugRequest(request);
                }
#endif

                request.BeginGetResponse(r =>
                    {
                        try
                        {
                            HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(r);
#if DEBUG
                            if (Options.HttpDebugging)
                            {
                                Tools.HttpDebugResponse(response);
                            }
#endif
                            response.Close();
                            callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.CreateStore, storeID), state);
                        }
                        catch (WebException webEx)
                        {
                            if (webEx.Response != null)
                            {
#if DEBUG
                                if (Options.HttpDebugging)
                                {
                                    if (webEx.Response != null) Tools.HttpDebugResponse((HttpWebResponse)webEx.Response);
                                }
#endif
                                //Got a Response so we can analyse the Response Code
                                HttpWebResponse response = (HttpWebResponse)webEx.Response;
                                int code = (int)response.StatusCode;
                                if (code == 400)
                                {
                                    //400 just means the store already exists so this is OK
                                    callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.CreateStore, storeID), state);
                                }
                                else
                                {
                                    callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.CreateStore, storeID, new RdfStorageException("A HTTP error occurred while trying to create a store, see inner exception for details", webEx)), state);
                                }
                            }
                            else
                            {
                                callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.CreateStore, storeID, new RdfStorageException("A HTTP error occurred while trying to create a store, see inner exception for details", webEx)), state);
                            }
                        }
                        catch (Exception ex)
                        {
                            callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.CreateStore, storeID, new RdfStorageException("An unexpected error occurred while trying to create a store, see inner exception for details", ex)), state);
                        }
                    }, state);
            }
            catch (WebException webEx)
            {
                if (webEx.Response != null)
                {
#if DEBUG
                    if (Options.HttpDebugging)
                    {
                        if (webEx.Response != null) Tools.HttpDebugResponse((HttpWebResponse)webEx.Response);
                    }
#endif
                    //Got a Response so we can analyse the Response Code
                    HttpWebResponse response = (HttpWebResponse)webEx.Response;
                    int code = (int)response.StatusCode;
                    if (code == 400)
                    {
                        //400 just means the store already exists so this is OK
                        callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.CreateStore, storeID), state);
                    }
                    else
                    {
                        callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.CreateStore, storeID, new RdfStorageException("A HTTP error occurred while trying to create a store, see inner exception for details", webEx)), state);
                    }
                }
                else
                {
                    callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.CreateStore, storeID, new RdfStorageException("A HTTP error occurred while trying to create a store, see inner exception for details", webEx)), state);
                }
            }
            catch (Exception ex)
            {
                callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.CreateStore, storeID, new RdfStorageException("An unexpected error occurred while trying to create a store, see inner exception for details", ex)), state);
            }
        }

        public override void DeleteStore(string storeID, AsyncStorageCallback callback, object state)
        {
            try
            {
                HttpWebRequest request = this.CreateRequest("repositories/" + this._store, "*/*", "DELETE", new Dictionary<string, string>());

#if DEBUG
                if (Options.HttpDebugging)
                {
                    Tools.HttpDebugRequest(request);
                }
#endif

                request.BeginGetResponse(r =>
                {
                    try
                    {
                        HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(r);
#if DEBUG
                        if (Options.HttpDebugging)
                        {
                            Tools.HttpDebugResponse(response);
                        }
#endif
                        //If we get here then the operation completed OK
                        response.Close();
                        callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.DeleteStore, storeID), state);
                    }
                    catch (WebException webEx)
                    {
#if DEBUG
                        if (Options.HttpDebugging)
                        {
                            if (webEx.Response != null) Tools.HttpDebugResponse((HttpWebResponse)webEx.Response);
                        }
#endif
                        callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.DeleteStore, storeID, new RdfStorageException("A HTTP Error occurred while attempting to delete a Store, see inner exception for details", webEx)), state);
                    }
                    catch (Exception ex)
                    {
                        callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.DeleteStore, storeID, new RdfStorageException("An unexpected error occurred while attempting to delete a Store, see inner exception for details", ex)), state);
                    }
                }, state);
            }
            catch (WebException webEx)
            {
#if DEBUG
                if (Options.HttpDebugging)
                {
                    if (webEx.Response != null) Tools.HttpDebugResponse((HttpWebResponse)webEx.Response);
                }
#endif
                callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.DeleteStore, storeID, new RdfStorageException("A HTTP Error occurred while attempting to delete a Store, see inner exception for details", webEx)), state);
            }
            catch (Exception ex)
            {
                callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.DeleteStore, storeID, new RdfStorageException("An unexpected error occurred while attempting to delete a Store, see inner exception for details", ex)), state);
            }
        }

        /// <summary>
        /// Gets a Store within the current catalog asynchronously
        /// </summary>
        /// <param name="storeID">Store ID</param>
        /// <returns></returns>
        /// <remarks>
        /// AllegroGraph groups stores by catalogue, you may only use this method to obtain stores within your current catalogue
        /// </remarks>
        public override void GetStore(string storeID, AsyncStorageCallback callback, object state)
        {
            if (this._store.Equals(storeID))
            {
                callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.GetStore, storeID, this), state);
            }
            else
            {
#if !NO_PROXY
                callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.GetStore, storeID, new AllegroGraphConnector(this._agraphBase, this._catalog, storeID, this._username, this._pwd, this.Proxy)), state);
#else
                callback(this, new AsyncStorageCallbackArgs(AsyncStorageOperation.GetStore, storeID, new AllegroGraphConnector(this._agraphBase, this._catalog, storeID, this._username, this._pwd)), state);
#endif
            }
        }
        

        /// <summary>
        /// Helper method for creating HTTP Requests to the Store
        /// </summary>
        /// <param name="servicePath">Path to the Service requested</param>
        /// <param name="accept">Acceptable Content Types</param>
        /// <param name="method">HTTP Method</param>
        /// <param name="queryParams">Querystring Parameters</param>
        /// <returns></returns>
        protected override HttpWebRequest CreateRequest(string servicePath, string accept, string method, Dictionary<string, string> queryParams)
        {
            //Remove JSON Mime Types from supported Accept types
            //This is a compatability issue with Allegro having a weird custom JSON serialisation
            if (accept.Contains("application/json"))
            {
                accept = accept.Replace("application/json,", String.Empty);
                if (accept.Contains(",,")) accept = accept.Replace(",,", ",");
            }
            if (accept.Contains("text/json"))
            {
                accept = accept.Replace("text/json", String.Empty);
                if (accept.Contains(",,")) accept = accept.Replace(",,", ",");
            }
            if (accept.Contains(",;")) accept = accept.Replace(",;", ",");

            return base.CreateRequest(servicePath, accept, method, queryParams);
        }

        /// <summary>
        /// Does nothing as AllegroGraph does not require the same query escaping that Sesame does
        /// </summary>
        /// <param name="query">Query to escape</param>
        /// <returns></returns>
        protected override string EscapeQuery(string query)
        {
            return query;
        }

        /// <summary>
        /// Gets a String which gives details of the Connection
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (this._catalog != null)
            {
                return "[AllegroGraph] Store '" + this._store + "' in Catalog '" + this._catalog + "' on Server '" + this._baseUri.Substring(0, this._baseUri.IndexOf("catalogs/")) + "'";
            }
            else
            {
                return "[AllegroGraph] Store '" + this._store + "' in Root Catalog on Server '" + this._baseUri + "'";
            }
        }

        /// <summary>
        /// Serializes the connection's configuration
        /// </summary>
        /// <param name="context">Configuration Serialization Context</param>
        public override void SerializeConfiguration(ConfigurationSerializationContext context)
        {
            INode manager = context.NextSubject;
            INode rdfType = context.Graph.CreateUriNode(UriFactory.Create(RdfSpecsHelper.RdfType));
            INode rdfsLabel = context.Graph.CreateUriNode(UriFactory.Create(NamespaceMapper.RDFS + "label"));
            INode dnrType = ConfigurationLoader.CreateConfigurationNode(context.Graph, ConfigurationLoader.PropertyType);
            INode genericManager = ConfigurationLoader.CreateConfigurationNode(context.Graph, ConfigurationLoader.ClassGenericManager);
            INode server = ConfigurationLoader.CreateConfigurationNode(context.Graph, ConfigurationLoader.PropertyServer);
            INode catalog = ConfigurationLoader.CreateConfigurationNode(context.Graph, ConfigurationLoader.PropertyCatalog);
            INode store = ConfigurationLoader.CreateConfigurationNode(context.Graph, ConfigurationLoader.PropertyStore);

            context.Graph.Assert(new Triple(manager, rdfType, genericManager));
            context.Graph.Assert(new Triple(manager, rdfsLabel, context.Graph.CreateLiteralNode(this.ToString())));
            context.Graph.Assert(new Triple(manager, dnrType, context.Graph.CreateLiteralNode(this.GetType().FullName)));
            if (this._catalog != null)
            {
                context.Graph.Assert(new Triple(manager, server, context.Graph.CreateLiteralNode(this._baseUri.Substring(0, this._baseUri.IndexOf("catalogs/")))));
                context.Graph.Assert(new Triple(manager, catalog, context.Graph.CreateLiteralNode(this._catalog)));
            }
            else
            {
                context.Graph.Assert(new Triple(manager, server, context.Graph.CreateLiteralNode(this._baseUri)));
            }
            context.Graph.Assert(new Triple(manager, store, context.Graph.CreateLiteralNode(this._store)));
            
            if (this._username != null && this._pwd != null)
            {
                INode username = ConfigurationLoader.CreateConfigurationNode(context.Graph, ConfigurationLoader.PropertyUser);
                INode pwd = ConfigurationLoader.CreateConfigurationNode(context.Graph, ConfigurationLoader.PropertyPassword);
                context.Graph.Assert(new Triple(manager, username, context.Graph.CreateLiteralNode(this._username)));
                context.Graph.Assert(new Triple(manager, pwd, context.Graph.CreateLiteralNode(this._pwd)));
            }

            base.SerializeProxyConfig(manager, context);
        }
    }
}
