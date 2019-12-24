//STORE_FRONT_VERSION 10.16+
using AspDotNetStorefront.Core;
//STORE_FRONT_VERSION 10.15-
//using AspDotNetStorefrontCore;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Xml.Serialization;

namespace AspDotNetStorefrontAdmin
{
    public partial class mocomaintenance : System.Web.UI.Page
    { 

        #region VARIABLES

        private const string QUERY_KEY_MAIN = "ID";
        private const string QUERY_KEY_DATE = "DATE";
        private const string QUERY_KEY_RUN = "RUNS";
        private const string QUERY_KEY_OVERRIDE_TIMEOUT_TASK = "OVERRIDE_TIMEOUT_TASK";
        private const string QUERY_KEY_OVERRIDE_TIMEOUT_URL = "OVERRIDE_TIMEOUT_URL";
        private const string DATE_FORMAT = "yyyy-MM-dd";
        private const string QUERY_PASSKEY = "MOCO_OCOM";
        private const bool SSL_NEEDED = false;
        private const int DEFAULT_OVERRIDE_TIMEOUT = 120;

        #region DeleteProfiles

        private const int DEFAULT_DP_DAYS_OLD = 10;

        #endregion

        #region DeleteShoppingCarts

        private const int DEFAULT_DSC_DAYS_OLD = 10;
        private const int DEFAULT_DSC_LOOP_MAX = 10;
        private const decimal DEFAULT_DSC_WAIT_TIME = 0.25M;
        private const int DEFAULT_DSC_DELETEBATCHCOUNT = 1000;

        #endregion

        #region DeleteGiftRegistries

        private const int DEFAULT_DGR_DAYS_OLD = 10;
        private const int DEFAULT_DGR_LOOP_MAX = 10;
        private const decimal DEFAULT_DGR_WAIT_TIME = 0.25M;
        private const int DEFAULT_DGR_DELETEBATCHCOUNT = 1000;

        #endregion

        #region DeleteCustomersPlus

        private const int DEFAULT_DCP_DAYS_OLD = 10;
        private const int DEFAULT_DCP_LOOP_MAX = 10;
        private const decimal DEFAULT_DCP_WAIT_TIME = 0.25M;
        private const int DEFAULT_DCP_DELETEBATCHCOUNT = 1000;

        #endregion

        #region CustomersWithoutOrdersAndEmails

        private const int DEFAULT_DCWOAE_DAYS_OLD = 180;
        private const int DEFAULT_DCWOAE_LOOP_MAX = 10;
        private const decimal DEFAULT_DCWOAE_WAIT_TIME = 0.25M;
        private const int DEFAULT_DCWOAE_DELETEBATCHCOUNT = 1000;

        #endregion

        #region DeleteCustomersWithoutOrders

        private const int DEFAULT_DCWO_DAYS_OLD = 180;
        private const int DEFAULT_DCWO_LOOP_MAX = 10;
        private const decimal DEFAULT_DCWO_WAIT_TIME = 0.25M;
        private const int DEFAULT_DCWO_DELETEBATCHCOUNT = 1000;

        #endregion

        #endregion

        #region METHODS

        #region PROTECTED

        protected void Page_Load(object sender, EventArgs e)
        {
            NameValueCollection queries = Request.QueryString;
            bool ssl = Request.Url.Scheme.ToLower().Equals("https");
            int statusCode = (int)HttpStatusCode.OK;
            LogCollection responseData = new LogCollection()
            {
                DateStart = DateTime.UtcNow,
                Finished = false,
                Key = getUrlQuery(queries),
                Result = 0,
                ServerTime = DateTime.Now,
                Value = "not valid",
                DateEnd = DateTime.MinValue,
                LogTypeName = "MOCO_MAINTENANCE",
                LogCollectionId = 0,
                VerionNumber = "1.03",
            };
             
            if ("GET" == this.Context.Request.HttpMethod)
            {
                if (validUrl(queries, ssl))
                {
                    Page.Session.Timeout = getValueOrDefaultInt(queries, QUERY_KEY_OVERRIDE_TIMEOUT_URL, 300);
                    int timeout = DEFAULT_OVERRIDE_TIMEOUT;
                    if (AppLogic.AppConfig("MonthlyMaintenanceTimeout") != string.Empty)
                        timeout = int.Parse(AppLogic.AppConfig("MonthlyMaintenanceTimeout"));
                    timeout = getValueOrDefaultInt(queries, QUERY_KEY_OVERRIDE_TIMEOUT_TASK, timeout) * 1000;

                    SqlConnection conn = null;
                    try
                    {

                        conn = new SqlConnection(DB.GetDBConn());
                        conn.Open();

                        runMaintenance(conn, queries, timeout, responseData);
                        responseData.Value = "valid";
                        responseData.Finished = true;

						if (AppConfigManager.AppConfigExists("NextMaintenanceDate"))
						{
							AppConfigManager.SetAppConfigValue("NextMaintenanceDate", DateTime.Now.AddMonths(1).ToString());
						} 
					}
                    catch (Exception ex)
                    {
                        responseData.Result = -1;
                        responseData.Value += getExceptionToLog(ex, 0, 2000);
                    }
                    finally
                    {
                        if (null != conn)
                        {
                            conn.Close();
                            conn.Dispose();
                        }
                    }
                }
            }

            finishLogEntry(responseData, responseData.Result); 
            updateResponse(this.Context, statusCode, responseData);

        }

        #region Response Data And Run

        protected virtual void runMaintenance(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {

            string myKey = getValueOrDefaultString(queries, QUERY_KEY_RUN, "");
            if (!string.IsNullOrWhiteSpace(myKey))
            {
                entry.Items = new List<LogEntry>();
                string[] myKeys = myKey.Split('|');
                int result = 0;

                foreach (var eachKey in myKeys)
                {
                    entry.Items.Add(createLogEntry(eachKey.ToUpper(), "", 0));
                    result = 0;

                    try
                    {
                        switch (eachKey.ToUpper())
                        {
                            case "STMM":
                                entry.Items[entry.Items.Count - 1].Value = "StoreFrontMonthlyMaintenance";
                                runMaintenanceStoreFrontMonthlyMaintenace(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "STDM":
                                entry.Items[entry.Items.Count - 1].Value = "StoreFrontDatabaseMaintenance";
                                runMaintenanceStoreFrontDatabaseMaintenance(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break; 
                            case "DCWO":
                                entry.Items[entry.Items.Count - 1].Value = "DeleteCustomersWithoutOrders";
                                runMaintenanceDeleteCustomersWithoutOrders(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "DI":
                                entry.Items[entry.Items.Count - 1].Value = "DefragIndexes";
                                runMaintenanceDefragIndexes(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "DCWOAE":
                                entry.Items[entry.Items.Count - 1].Value = "CustomersWithoutOrdersAndEmails";
                                runMaintenanceDeleteCustomersWithoutOrdersAndEmails(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "DP":
                                entry.Items[entry.Items.Count - 1].Value = "DeleteProfiles";
                                runMaintenanceDeleteProfiles(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "DSC":
                                entry.Items[entry.Items.Count - 1].Value = "DeleteShoppingCarts";
                                runMaintenanceDeleteShoppingCarts(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "DGR":
                                entry.Items[entry.Items.Count - 1].Value = "DeleteGiftRegistries";
                                runMaintenanceDeleteGiftRegistries(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "DCP":
                                entry.Items[entry.Items.Count - 1].Value = "DeleteCustomersPlus";
                                runMaintenanceDeleteCustomersPlus(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "SPL_0":
                                entry.Items[entry.Items.Count - 1].Value = "SPECIAL";
                                runMaintenanceSpecial0(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "SPL_1":
                                entry.Items[entry.Items.Count - 1].Value = "SPECIAL";
                                runMaintenanceSpecial1(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "SPL_2":
                                entry.Items[entry.Items.Count - 1].Value = "SPECIAL";
                                runMaintenanceSpecial2(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "SPL_3":
                                entry.Items[entry.Items.Count - 1].Value = "SPECIAL";
                                runMaintenanceSpecial3(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "SPL_4":
                                entry.Items[entry.Items.Count - 1].Value = "SPECIAL";
                                runMaintenanceSpecial4(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "SPL_5":
                                entry.Items[entry.Items.Count - 1].Value = "SPECIAL";
                                runMaintenanceSpecial5(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "SPL_6":
                                entry.Items[entry.Items.Count - 1].Value = "SPECIAL";
                                runMaintenanceSpecial6(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "SPL_7":
                                entry.Items[entry.Items.Count - 1].Value = "SPECIAL";
                                runMaintenanceSpecial7(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "SPL_8":
                                entry.Items[entry.Items.Count - 1].Value = "SPECIAL";
                                runMaintenanceSpecial8(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                            case "SPL_9":
                                entry.Items[entry.Items.Count - 1].Value = "SPECIAL";
                                runMaintenanceSpecial9(conn, queries, timeout, entry.Items[entry.Items.Count - 1]);
                                break;
                        }
                    }
                    catch (SqlException ex)
                    {
                        result = -1;
                        ex.Data.Add("runMaintenance__ex_LineNumber", ex.LineNumber);
                        ex.Data.Add("runMaintenance__ex_Number", ex.Number);
                        ex.Data.Add("runMaintenance__ex_type", ex.GetType().ToString());
                        ex.Data.Add("runMaintenance__ex_StackTrace", ex.StackTrace);
                        ex.Data.Add("runMaintenance__ex_Source", ex.Source);
                        throw new Exception(ex.Message, ex);
                    }
                    catch (Exception ex)
                    {
                        result = -1;
                        ex.Data.Add("runMaintenance__ex_type", ex.GetType().ToString());
                        ex.Data.Add("runMaintenance__ex_StackTrace", ex.StackTrace);
                        ex.Data.Add("runMaintenance__ex_Source", ex.Source);
                        throw new Exception(ex.Message, ex);

                    }
                    finally
                    {
                        finishLogEntry(entry.Items[entry.Items.Count - 1], result);
                    }
                }
            }

        }

        protected virtual void runMaintenanceStoreFrontMonthlyMaintenace(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {
            runMaintenanceStoreProcedure(
              conn,
              getStoreFrontDefaultMaintenanceParameters().ToArray(), //getSqlParameters(queries,"stmm_")
              timeout,
              "dbo.aspdnsf_MonthlyMaintenance", entry);
        }

        protected virtual void runMaintenanceStoreFrontDatabaseMaintenance(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {
            runMaintenanceStoreProcedure(
              conn,
              getStoreFrontDefaultMaintenanceParameters().ToArray(), //getSqlParameters(queries,"stmm_")
              timeout,
              "dbo.aspdnsf_DatabaseMaintenance", entry);
        }

        protected virtual void runMaintenanceDeleteCustomersWithoutOrders(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {
            StringBuilder returnValue = new StringBuilder();

            int daysOld = getValueOrDefaultInt(queries, "DCWO_DAYS_OLD", DEFAULT_DCWO_DAYS_OLD);
            int loopMax = getValueOrDefaultInt(queries, "DCWO_LOOP_MAX", DEFAULT_DCWO_LOOP_MAX);
            decimal waitTime = getValueOrDefaultDecimal(queries, "DCWO_WAIT_TIME", DEFAULT_DCWO_WAIT_TIME);
            int deleteBatchCount = getValueOrDefaultInt(queries, "DCWO_DELETEBATCHCOUNT", DEFAULT_DCWO_DELETEBATCHCOUNT);

            entry.Value += "\r\n" + "daysOld:" + daysOld + "\r\n" + "loopMax:" + loopMax + "\r\n" + "waitTime:" + waitTime + "\r\n" + "deleteBatchCount:" + deleteBatchCount;
            entry.Items = new List<LogEntry>();

            if (daysOld >= DEFAULT_DCWO_DAYS_OLD)
            {
                bool existTableCustomer = doesTableExist(conn, "dbo", "Customer", timeout);
                bool existTableOrders = doesTableExist(conn, "dbo", "Orders", timeout);

                if (existTableCustomer && existTableOrders)
                {
                    String customerSql = string.Format(@"DELETE TOP({1}) FROM Customer
														WHERE NOT EXISTS (
                                                            SELECT NULL 
                                                            FROM Orders 
                                                            WHERE Customer.CustomerID = Orders.CustomerID)
														AND DATEDIFF(DAY, Customer.CreatedOn, GETDATE()) > {0}
														AND Customer.IsAdmin = 0 "
                                                + (doesTableExist(conn, "dbo", "rating", timeout) ? " AND CustomerID NOT IN (select customerid from dbo.rating with (NOLOCK))" : "")
                                                + (doesTableExist(conn, "dbo", "ratingcommenthelpfulness", timeout) ? " AND CustomerID NOT IN (select ratingcustomerid from dbo.ratingcommenthelpfulness with (NOLOCK)) AND CustomerID NOT IN (select votingcustomerid from dbo.ratingcommenthelpfulness with (NOLOCK)) " : "")
                                                + (doesTableExist(conn, "dbo", "pollvotingrecord", timeout) ? " AND CustomerID NOT IN (select customerid from dbo.pollvotingrecord with (NOLOCK))" : "")
                                                , daysOld, deleteBatchCount);

                    runMaintenanceNonQueryLooped(conn, customerSql, timeout, loopMax, waitTime, deleteBatchCount, entry);

                }
                else
                {
                    if (!existTableCustomer)
                    {
                        entry.Value = "\r\n" + "dbo" + "." + "Address" + "_DOES_NOT_EXIST";
                        finishLogEntry(entry, -1);
                    }

                    if (!existTableOrders)
                    {
                        entry.Value = "\r\n" + "dbo" + "." + "Orders" + "_DOES_NOT_EXIST";
                        finishLogEntry(entry, -1);
                    }
                }
            }

        }

		protected virtual void runMaintenanceDefragIndexes(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
		{ 

			string breaker = "\r\n";
			StringBuilder sbQuery = new StringBuilder("DECLARE @cmd varchar(8000)");
			sbQuery.Append(breaker + "CREATE TABLE #SHOWCONTIG (");
			sbQuery.Append(breaker + "	tblname VARCHAR(255),");
			sbQuery.Append(breaker + "	ObjectId INT,");
			sbQuery.Append(breaker + "	IndexName VARCHAR(255),");
			sbQuery.Append(breaker + "	IndexId INT,");
			sbQuery.Append(breaker + "	Lvl INT,");
			sbQuery.Append(breaker + "	CountPages INT,");
			sbQuery.Append(breaker + "	CountRows INT,");
			sbQuery.Append(breaker + "	MinRecSize INT,");
			sbQuery.Append(breaker + "	MaxRecSize INT,");
			sbQuery.Append(breaker + "	AvgRecSize INT,");
			sbQuery.Append(breaker + "	ForRecCount INT,");
			sbQuery.Append(breaker + "	Extents INT,");
			sbQuery.Append(breaker + "	ExtentSwitches INT,");
			sbQuery.Append(breaker + "	AvgFreeBytes INT,");
			sbQuery.Append(breaker + "	AvgPageDensity INT,");
			sbQuery.Append(breaker + "	ScanDensity DECIMAL,");
			sbQuery.Append(breaker + "	BestCount INT,");
			sbQuery.Append(breaker + "	ActualCount INT,");
			sbQuery.Append(breaker + "	LogicalFrag DECIMAL,");
			sbQuery.Append(breaker + "	ExtentFrag DECIMAL)");
			sbQuery.Append(breaker);
			sbQuery.Append(breaker + "SELECT TABLE_SCHEMA +'.' + TABLE_NAME as tblname INTO #tmp FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME ");
			sbQuery.Append(breaker);
			sbQuery.Append(breaker + "DECLARE @tblname varchar(255), @indexname varchar(255)");
			sbQuery.Append(breaker + "SELECT top 1 @tblname = tblname FROM #tmp");
			sbQuery.Append(breaker + "WHILE @@rowcount > 0 BEGIN");
			sbQuery.Append(breaker + "	SET @cmd = 'DBCC SHOWCONTIG (''' + @tblname + ''') with tableresults, ALL_INDEXES'");
			sbQuery.Append(breaker + "	INSERT #SHOWCONTIG");
			sbQuery.Append(breaker + "	EXEC(@cmd)");
			sbQuery.Append(breaker + "	DELETE #tmp WHERE tblname = @tblname ");
			sbQuery.Append(breaker + "	SELECT top 1 @tblname = tblname FROM #tmp");
			sbQuery.Append(breaker + "END");
			sbQuery.Append(breaker);
			sbQuery.Append(breaker + "DELETE #SHOWCONTIG WHERE LogicalFrag < 5 or Extents = 1 or IndexId in (0, 255)");
			sbQuery.Append(breaker);
			sbQuery.Append(breaker + "SELECT tblname, IndexName FROM #SHOWCONTIG  ORDER BY IndexId;");
			sbQuery.Append(breaker + "DROP TABLE #SHOWCONTIG; ");
			sbQuery.Append(breaker + "DROP TABLE #tmp; ");
			
			IDataReader reader = null;
			int result = 0;
             
			Dictionary<string, string> indexes = new Dictionary<string, string>();

			try
			{

				using (var command = new SqlCommand(sbQuery.ToString()))
				{
					command.Connection = conn;
					command.CommandTimeout = timeout;

					using (reader = command.ExecuteReader())
					{
						while (reader.Read())
						{
							if (!(reader["tblname"] is DBNull) && (reader["tblname"] != null) && !(reader["IndexName"] is DBNull) && (reader["IndexName"] != null))
							{
								if (!indexes.ContainsKey(reader["IndexName"].ToString()))
								{
									indexes.Add(reader["IndexName"].ToString(), reader["tblname"].ToString());
								}
							}
						}
					}
				}

				result = indexes.Count;
			}
			catch (Exception ex)
			{
				entry.Value = ex.Message;
				result = -1;
			}
			finally
			{
				finishLogEntry(entry, result);
			}

            entry.Items = new List<LogEntry>();
            LogEntry logEntry = null;
			foreach (var eachKey in indexes.Keys)
			{
				try
				{
					logEntry = createLogEntry(entry.Key + eachKey, eachKey, 0);
					runMaintenanceNonQuery(conn, "DBCC DBREINDEX ('" + indexes[eachKey] + "', '" + eachKey + "', 90)  ", timeout, logEntry);
				}
				finally
				{
					entry.Items.Add(logEntry);
				}
			}

		}

        protected virtual void runMaintenanceDeleteCustomersWithoutOrdersAndEmails(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {

            int daysOld = getValueOrDefaultInt(queries, "DCWOAE_DAYS_OLD", DEFAULT_DCWOAE_DAYS_OLD);
            int loopMax = getValueOrDefaultInt(queries, "DCWOAE_LOOP_MAX", DEFAULT_DCWOAE_LOOP_MAX);
            decimal waitTime = getValueOrDefaultDecimal(queries, "DCWOAE_WAIT_TIME", DEFAULT_DCWOAE_WAIT_TIME);
            int deleteBatchCount = getValueOrDefaultInt(queries, "DCWOAE_DELETEBATCHCOUNT", DEFAULT_DCWOAE_DELETEBATCHCOUNT);

            entry.Value += "\r\n" + "daysOld:" + daysOld + "\r\n" + "loopMax:" + loopMax + "\r\n" + "waitTime:" + waitTime + "\r\n" + "deleteBatchCount:" + deleteBatchCount;
            entry.Items = new List<LogEntry>();

            if (daysOld >= DEFAULT_DCWOAE_DAYS_OLD)
            {
                bool existTableCustomer = doesTableExist(conn, "dbo", "Customer", timeout);
                bool existTableOrders = doesTableExist(conn, "dbo", "Orders", timeout);
                bool existTableShoppingCart = doesTableExist(conn, "dbo", "ShoppingCart", timeout);

                if (existTableCustomer && existTableOrders & existTableShoppingCart)
                {
                    String customerSql = string.Format(@"DELETE TOP({1}) 
                                                    FROM Customer 
                                                    WHERE NOT EXISTS (
													    SELECT NULL 
                                                        FROM Orders O 
                                                        WHERE O.CustomerID = Customer.CustomerID
													)
                                                    AND (NULLIF(Email, '') IS NULL OR IsRegistered = 0)
													AND NOT EXISTS (
                                                        SELECT NULL 
                                                        FROM ShoppingCart 
                                                        WHERE ShoppingCart.CustomerID = Customer.CustomerID 
													    AND DATEDIFF(DAY, ShoppingCart.CreatedOn, GETDATE()) > {0}
                                                    )
													AND DATEDIFF(DAY, Customer.CreatedOn, GETDATE()) > {0}"
                                                + (doesTableExist(conn, "dbo", "rating", timeout) ? " AND CustomerID NOT IN (select customerid from dbo.rating with (NOLOCK))" : "")
                                                + (doesTableExist(conn, "dbo", "ratingcommenthelpfulness", timeout) ? " AND CustomerID NOT IN (select ratingcustomerid from dbo.ratingcommenthelpfulness with (NOLOCK)) AND CustomerID NOT IN (select votingcustomerid from dbo.ratingcommenthelpfulness with (NOLOCK)) " : "")
                                                + (doesTableExist(conn, "dbo", "pollvotingrecord", timeout) ? " AND CustomerID NOT IN (select customerid from dbo.pollvotingrecord with (NOLOCK))" : "")
                                                , daysOld, deleteBatchCount);
                    runMaintenanceNonQueryLooped(conn, customerSql, timeout, loopMax, waitTime, deleteBatchCount, entry);

                }
                else
                {
                    if (!existTableCustomer)
                    {
                        entry.Value = "\r\n" + "dbo" + "." + "Address" + "_DOES_NOT_EXIST";
                        finishLogEntry(entry, -1);
                    }

                    if (!existTableOrders)
                    {
                        entry.Value = "\r\n" + "dbo" + "." + "Orders" + "_DOES_NOT_EXIST";
                        finishLogEntry(entry, -1);
                    }

                    if (!existTableShoppingCart)
                    {
                        entry.Value = "\r\n" + "dbo" + "." + "ShoppingCart" + "_DOES_NOT_EXIST";
                        finishLogEntry(entry, -1);
                    }
                }
            }

        }

        protected virtual void runMaintenanceDeleteCustomersPlus(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {

            int daysOld = getValueOrDefaultInt(queries, "DCP_DAYS_OLD", DEFAULT_DCP_DAYS_OLD);
            int loopMax = getValueOrDefaultInt(queries, "DCP_LOOP_MAX", DEFAULT_DCP_LOOP_MAX);
            decimal waitTime = getValueOrDefaultDecimal(queries, "DCP_WAIT_TIME", DEFAULT_DCP_WAIT_TIME);
            int deleteBatchCount = getValueOrDefaultInt(queries, "DCP_DELETEBATCHCOUNT", DEFAULT_DCP_DELETEBATCHCOUNT);

            entry.Value += "\r\n" + "daysOld:" + daysOld + "\r\n" + "loopMax:" + loopMax + "\r\n" + "waitTime:" + waitTime + "\r\n" + "deleteBatchCount:" + deleteBatchCount;
            entry.Items = new List<LogEntry>();

            if (daysOld >= DEFAULT_DCP_DAYS_OLD)
            {
                if (doesTableExist(conn, "dbo", "Customer", timeout))
                {
                    entry.Items.Add(createLogEntry(entry.Key + "_Address", "", 0));

                    if (doesTableExist(conn, "dbo", "Address", timeout)) 
                    {
                        String customerSql = string.Format(@"DELETE FROM Address WHERE AddressID IN (SELECT TOP({0}) AddressID FROM Address WHERE NOT EXISTS (SELECT NULL FROM Customer C WHERE Address.AddressID = C.BillingAddressID OR Address.AddressID = C.ShippingAddressID ) AND UpdatedOn < DATEADD(DAY, -{1}, GETDATE()) ORDER BY AddressID ASC)", deleteBatchCount, daysOld);
                        runMaintenanceNonQueryLooped(conn, customerSql, timeout, loopMax, waitTime, deleteBatchCount, entry.Items[entry.Items.Count - 1]);
                    }
                    else
                    {
                        entry.Items[entry.Items.Count - 1].Value = "dbo" + "." + "Address" + "_DOES_NOT_EXIST";
                        finishLogEntry(entry.Items[entry.Items.Count - 1], -1);
                    }

                    entry.Items.Add(createLogEntry(entry.Key + "_ShoppingCart", "", 0));

                    if (doesTableExist(conn, "dbo", "ShoppingCart", timeout))
                    {
                        String customerSql = string.Format(@"DELETE FROM ShoppingCart WHERE ShoppingCartRecID IN (SELECT TOP({0}) ShoppingCartRecID FROM ShoppingCart WHERE NOT EXISTS (SELECT NULL FROM Customer WHERE CustomerID = ShoppingCart.CustomerID) AND UpdatedOn < DATEADD(DAY, -{1}, GETDATE()) ORDER BY ShoppingCartRecID ASC)", deleteBatchCount, daysOld);
                        runMaintenanceNonQueryLooped(conn, customerSql, timeout, loopMax, waitTime, deleteBatchCount, entry.Items[entry.Items.Count - 1]);
                    }
                    else
                    {
                        entry.Items[entry.Items.Count - 1].Value = "dbo" + "." + "ShoppingCart" + "_DOES_NOT_EXIST";
                        finishLogEntry(entry.Items[entry.Items.Count - 1], -1);
                    }

                    entry.Items.Add(createLogEntry(entry.Key + "_KitCart", "", 0));

                    if (doesTableExist(conn, "dbo", "KitCart", timeout))
                    {
                        String customerSql = string.Format(@"DELETE FROM KitCart WHERE KitCartRecID IN (SELECT TOP({0}) KitCartRecID FROM KitCart WHERE NOT EXISTS (SELECT NULL FROM Customer WHERE Customer.CustomerID = KitCart.CustomerID) AND UpdatedOn < DATEADD(DAY, -{1}, GETDATE()) ORDER BY KitCartRecID ASC)", deleteBatchCount, daysOld);
                        runMaintenanceNonQueryLooped(conn, customerSql, timeout, loopMax, waitTime, deleteBatchCount, entry.Items[entry.Items.Count - 1]);
                    }
                    else
                    {
                        entry.Items[entry.Items.Count - 1].Value = "dbo" + "." + "KitCart" + "_DOES_NOT_EXIST";
                        finishLogEntry(entry.Items[entry.Items.Count - 1], -1);
                    }

                    entry.Items.Add(createLogEntry(entry.Key + "_CustomCart", "", 0));

                    if (doesTableExist(conn, "dbo", "CustomCart", timeout))
                    {
                        String customerSql = string.Format(@"DELETE TOP({0}) FROM CustomCart WHERE NOT EXISTS (SELECT NULL FROM Customer WHERE Customer.CustomerID = CustomCart.CustomerID) AND UpdatedOn < DATEADD(DAY, -{1}, GETDATE()) ", deleteBatchCount, daysOld);
                        runMaintenanceNonQueryLooped(conn, customerSql, timeout, loopMax, waitTime, deleteBatchCount, entry.Items[entry.Items.Count - 1]);
                    }
                    else
                    {
                        entry.Items[entry.Items.Count - 1].Value = "dbo" + "." + "CustomCart" + "_DOES_NOT_EXIST";
                        finishLogEntry(entry.Items[entry.Items.Count - 1], -1);
                    }
                }
                else
                {
                    entry.Items[entry.Items.Count - 1].Value = "dbo" + "." + "Customer" + "_DOES_NOT_EXIST";
                    finishLogEntry(entry.Items[entry.Items.Count - 1], -1);
                }

            }

        }

        protected virtual void runMaintenanceDeleteProfiles(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {

            int daysOld = getValueOrDefaultInt(queries, "DP_DAYS_OLD", DEFAULT_DP_DAYS_OLD);

            entry.Value += "\r\n" + "daysOld:" + daysOld;
            entry.Items = new List<LogEntry>();

            if (daysOld >= DEFAULT_DP_DAYS_OLD)
            {
                entry.Items.Add(createLogEntry(entry.Key + "_Profile", "", 0));

                if (doesTableExist(conn, "dbo", "Profile", timeout))
                {
                    var customerSql = string.Format(@"DELETE FROM dbo.Profile WHERE UpdatedOn <= DATEADD(day, {0},  GETDATE())", daysOld);
                    runMaintenanceNonQuery(conn, customerSql, timeout, entry.Items[entry.Items.Count - 1]);
                }
                else
                {
                    entry.Items[entry.Items.Count - 1].Value = "dbo" + "." + "Profile" + "_DOES_NOT_EXIST";
                    finishLogEntry(entry.Items[entry.Items.Count - 1], -1);
                }
            }

        }

        protected virtual void runMaintenanceDeleteShoppingCarts(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {

            int daysOld = getValueOrDefaultInt(queries, "DSC_DAYS_OLD", DEFAULT_DSC_DAYS_OLD);
            int loopMax = getValueOrDefaultInt(queries, "DSC_LOOP_MAX", DEFAULT_DSC_LOOP_MAX);
            decimal waitTime = getValueOrDefaultDecimal(queries, "DSC_WAIT_TIME", DEFAULT_DSC_WAIT_TIME);
            int deleteBatchCount = getValueOrDefaultInt(queries, "DSC_DELETEBATCHCOUNT", DEFAULT_DSC_DELETEBATCHCOUNT);

            entry.Value += "\r\n" + "daysOld:" + daysOld + "\r\n" + "loopMax:" + loopMax + "\r\n" + "waitTime:" + waitTime + "\r\n" + "deleteBatchCount:" + deleteBatchCount;
            entry.Items = new List<LogEntry>();

            if (daysOld >= DEFAULT_DSC_DAYS_OLD)
            {

                entry.Items.Add(createLogEntry(entry.Key + "_ShoppingCart", "", 0));

                if (doesTableExist(conn, "dbo", "ShoppingCart", timeout))
                {
                    var customerSql = string.Format(@"DELETE 
                    FROM dbo.ShoppingCart 
                    WHERE ShoppingCartRecID IN (
	                    SELECT TOP ({0}) ShoppingCartRecID
	                    FROM dbo.ShoppingCart 
	                    WHERE DATEDIFF(DAY, CreatedOn, GETDATE()) > {1}
                            AND (0 = CartType OR 101 = CartType)
	                    ORDER BY CreatedOn
                    )", daysOld, deleteBatchCount);

                    runMaintenanceNonQueryLooped(conn, customerSql, timeout, loopMax, waitTime, deleteBatchCount, entry.Items[entry.Items.Count - 1]);
                }
                else
                {
                    entry.Items[entry.Items.Count - 1].Value = "dbo" + "." + "ShoppingCart" + "_DOES_NOT_EXIST";
                    finishLogEntry(entry.Items[entry.Items.Count - 1], -1);
                }

                entry.Items.Add(createLogEntry(entry.Key + "_KitCart", "", 0));

                if (doesTableExist(conn, "dbo", "KitCart", timeout))
                {
                    var customerSql = string.Format(@"DELETE 
                    FROM dbo.KitCart 
                    WHERE KitCartRecID IN (
	                    SELECT TOP ({0}) KitCartRecID
	                    FROM dbo.KitCart 
	                    WHERE DATEDIFF(DAY, CreatedOn, GETDATE()) > {1}
                            AND (0 = CartType OR 101 = CartType)
	                    ORDER BY CreatedOn
                    )", daysOld, deleteBatchCount);

                    runMaintenanceNonQueryLooped(conn, customerSql, timeout, loopMax, waitTime, deleteBatchCount, entry.Items[entry.Items.Count - 1]);
                }
                else
                {
                    entry.Items[entry.Items.Count - 1].Value = "dbo" + "." + "KitCart" + "_DOES_NOT_EXIST";
                    finishLogEntry(entry.Items[entry.Items.Count - 1], -1);
                }

                entry.Items.Add(createLogEntry(entry.Key + "_CustomCart", "", 0));

                if (doesTableExist(conn, "dbo", "CustomCart", timeout))
                {
                    var customerSql = string.Format(@"DELETE 
                    FROM dbo.CustomCart 
                    WHERE CustomCartRecID IN (
	                    SELECT TOP ({0}) CustomCartRecID
	                    FROM dbo.CustomCart 
	                    WHERE DATEDIFF(DAY, CreatedOn, GETDATE()) > {1}
                            AND (0 = CartType OR 101 = CartType)
	                    ORDER BY CreatedOn
                    )", daysOld, deleteBatchCount);

                    runMaintenanceNonQueryLooped(conn, customerSql, timeout, loopMax, waitTime, deleteBatchCount, entry.Items[entry.Items.Count - 1]);
                }
                else
                {
                    entry.Items[entry.Items.Count - 1].Value = "dbo" + "." + "CustomCart" + "_DOES_NOT_EXIST";
                    finishLogEntry(entry.Items[entry.Items.Count - 1], -1);
                }

            }

        }

        protected virtual void runMaintenanceDeleteGiftRegistries(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {

            int daysOld = getValueOrDefaultInt(queries, "DGR_DAYS_OLD", DEFAULT_DGR_DAYS_OLD);
            int loopMax = getValueOrDefaultInt(queries, "DGR_LOOP_MAX", DEFAULT_DGR_LOOP_MAX);
            decimal waitTime = getValueOrDefaultDecimal(queries, "DGR_WAIT_TIME", DEFAULT_DGR_WAIT_TIME);
            int deleteBatchCount = getValueOrDefaultInt(queries, "DGR_DELETEBATCHCOUNT", DEFAULT_DGR_DELETEBATCHCOUNT);

            entry.Value += "\r\n" + "daysOld:" + daysOld + "\r\n" + "loopMax:" + loopMax + "\r\n" + "waitTime:" + waitTime + "\r\n" + "deleteBatchCount:" + deleteBatchCount;
            entry.Items = new List<LogEntry>();
            if (daysOld >= DEFAULT_DGR_DAYS_OLD)
            {

                entry.Items.Add(createLogEntry(entry.Key + "_ShoppingCart", "", 0));

                if (doesTableExist(conn, "dbo", "ShoppingCart", timeout))
                {
                    var customerSql = string.Format(@"DELETE 
                    FROM dbo.ShoppingCart 
                    WHERE ShoppingCartRecID IN (
	                    SELECT TOP ({0}) ShoppingCartRecID
	                    FROM dbo.ShoppingCart 
	                    WHERE DATEDIFF(DAY, CreatedOn, GETDATE()) > {1}
                            AND (0 = CartType)
	                    ORDER BY CreatedOn
                    )", daysOld, deleteBatchCount);

                    runMaintenanceNonQueryLooped(conn, customerSql, timeout, loopMax, waitTime, deleteBatchCount, entry.Items[entry.Items.Count - 1]);
                }
                else
                {
                    entry.Items[entry.Items.Count - 1].Value = "dbo" + "." + "ShoppingCart" + "_DOES_NOT_EXIST";
                    finishLogEntry(entry.Items[entry.Items.Count - 1], -1);
                }

                entry.Items.Add(createLogEntry(entry.Key + "_KitCart", "", 0));

                if (doesTableExist(conn, "dbo", "KitCart", timeout))
                {
                    var customerSql = string.Format(@"DELETE 
                    FROM dbo.KitCart 
                    WHERE KitCartRecID IN (
	                    SELECT TOP ({0}) KitCartRecID
	                    FROM dbo.KitCart 
	                    WHERE DATEDIFF(DAY, CreatedOn, GETDATE()) > {1}
                            AND (0 = CartType)
	                    ORDER BY CreatedOn
                    )", daysOld, deleteBatchCount);

                    runMaintenanceNonQueryLooped(conn, customerSql, timeout, loopMax, waitTime, deleteBatchCount, entry.Items[entry.Items.Count - 1]);
                }
                else
                {
                    entry.Items[entry.Items.Count - 1].Value = "dbo" + "." + "KitCart" + "_DOES_NOT_EXIST";
                    finishLogEntry(entry.Items[entry.Items.Count - 1], -1);
                }

                entry.Items.Add(createLogEntry(entry.Key + "_CustomCart", "", 0));

                if (doesTableExist(conn, "dbo", "CustomCart", timeout))
                {
                    var customerSql = string.Format(@"DELETE 
                    FROM dbo.CustomCart 
                    WHERE CustomCartRecID IN (
	                    SELECT TOP ({0}) CustomCartRecID
	                    FROM dbo.CustomCart 
	                    WHERE DATEDIFF(DAY, CreatedOn, GETDATE()) > {1}
                            AND (0 = CartType)
	                    ORDER BY CreatedOn
                    )", daysOld, deleteBatchCount);

                    runMaintenanceNonQueryLooped(conn, customerSql, timeout, loopMax, waitTime, deleteBatchCount, entry.Items[entry.Items.Count - 1]);
                }
                else
                {
                    entry.Items[entry.Items.Count - 1].Value = "dbo" + "." + "CustomCart" + "_DOES_NOT_EXIST";
                    finishLogEntry(entry.Items[entry.Items.Count - 1], -1);
                }
            }

        }

        #region SPECIAL

        protected virtual void runMaintenanceSpecial0(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {
              
        }

        protected virtual void runMaintenanceSpecial1(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {

        }

        protected virtual void runMaintenanceSpecial2(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {

        }

        protected virtual void runMaintenanceSpecial3(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {

        }

        protected virtual void runMaintenanceSpecial4(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {

        }

        protected virtual void runMaintenanceSpecial5(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {

        }

        protected virtual void runMaintenanceSpecial6(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {

        }

        protected virtual void runMaintenanceSpecial7(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {
        }

        protected virtual void runMaintenanceSpecial8(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {

        }

        protected virtual void runMaintenanceSpecial9(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {

        }

        #endregion

        #region DATA RETENTION

        protected virtual List<int> getInActiveCustomerIdsWithoutOrders(SqlConnection conn, NameValueCollection queries, int timeout, LogEntry entry)
        {
            List<int> returnValue = new List<int>();

            bool doesCustomerDataRetentionExist = doesTableExist(conn, "dbo", "CustomerDataRetention", timeout);
            bool doesCustomerExist = doesTableExist(conn, "dbo", "Customer", timeout);
            int daysOld = getValueOrDefaultInt(queries, "DRICWO_DAYS_OLD", DEFAULT_DCWOAE_DAYS_OLD);

            if (doesCustomerExist && doesCustomerDataRetentionExist)
            {

            }


            return returnValue;
        }

		#endregion

		#endregion

		#region SQL 

		protected virtual List<string> getListOfStringFromQuery(SqlConnection conn, string key, string queryCommand, string column, int timeout, out LogEntry entry)
		{
			List<string> returnValue = new List<string>();

			IDataReader reader = null; 
			int result = 0;

			entry = createLogEntry(key, column, 0);
			try
			{
				using (var command = new SqlCommand(queryCommand))
				{
					command.Connection = conn;
					command.CommandTimeout = timeout;

					reader = command.ExecuteReader();
					while (reader.Read())
					{
						if (!(reader[column] is DBNull)
							&& (reader[column] != null))
						{
							returnValue.Add(reader[column].ToString());
						}
					}

				}
				result = returnValue.Count;
			}
			catch (Exception ex)
			{
				entry.Value = ex.Message;
				result = -1;
			}
			finally
			{
				finishLogEntry(entry, result);
			}


			return returnValue;
		}

		protected virtual List<int> getListOfIntFromQuery(SqlConnection conn, string key, string queryCommand, string column, int timeout, out LogEntry entry)
        {
            List<int> returnValue = new List<int>();

            IDataReader reader = null;
            int tempTest = 0;
            int result = 0;

            entry = createLogEntry(key, column, 0);
            try
            {
                using (var command = new SqlCommand(queryCommand))
                {
                    command.Connection = conn;
                    command.CommandTimeout = timeout;

                    reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        if (!(reader[column] is DBNull) 
                            && (reader[column] != null) 
                            && int.TryParse(reader[column].ToString(), out tempTest))
                        {
                            returnValue.Add(tempTest);
                        }
                    }
                     
                }
                result = returnValue.Count;
            }
            catch(Exception ex)
            {
                entry.Value = ex.Message;
                result = -1;
            }
            finally
            {
                finishLogEntry(entry, result);
            }


            return returnValue;
        }

        protected virtual bool doesTableExist(SqlConnection conn, string schemaName, string tableName, int timeout)
        {
            bool returnValue = false;

            LogEntry entry = createLogEntry(schemaName + "." + tableName, "doesTableExist", 0);

            try
            {
                returnValue = ("1" == getExecuteScalar(conn, string.Format(@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{0}' AND TABLE_NAME = '{1}'", schemaName, tableName), timeout, "0", entry));
            }
            catch (Exception) { }
            finally
            {
                finishLogEntry(entry, -1);
            }

            return returnValue;
        }

        protected virtual bool doesStoreProcedure(SqlConnection conn, string schemaName, string storeProcedure, int timeout)
        {
            bool returnValue = false;

            LogEntry entry = createLogEntry(schemaName + "." + storeProcedure, "doesStoreProcedureExist", 0);

            try
            {
                returnValue = ("1" == getExecuteScalar(conn, string.Format(@"SELECT CASE WHEN myId IS NULL THEN 0 ELSE 1 END FROM (SELECT OBJECT_ID('{0}', 'P') AS myId) as fool", storeProcedure), timeout, "0", entry));
            }
            catch (Exception) { }
            finally
            {
                finishLogEntry(entry, -1);
            }

            return returnValue;
		}

		protected virtual int getExecuteScalarInt(SqlConnection conn, string query, int timeout, int defaultValue, LogEntry entry)
		{
			int returnValue = defaultValue;

			entry.Value += "\r\n" + "timeout:" + timeout + "\r\n" + "defaultValue:" + defaultValue;

			try
			{
				using (var command = new SqlCommand(query))
				{
					command.Connection = conn;
					command.CommandTimeout = timeout;
					object myObject = command.ExecuteScalar();

					if (!(myObject is DBNull) && null != myObject)
					{
						string temp = myObject.ToString();
						int.TryParse(temp, out returnValue);
					}
				}
			}
			finally
			{
				finishLogEntry(entry, 0);
			}

			return returnValue;
		}

		protected virtual string getExecuteScalar(SqlConnection conn, string query, int timeout, string defaultValue, LogEntry entry)
        {
            string returnValue = "";

            entry.Value += "\r\n" + "timeout:" + timeout + "\r\n" + "defaultValue:" + defaultValue;

            try
            {
                using (var command = new SqlCommand(query))
                {
                    command.Connection = conn;
                    command.CommandTimeout = timeout;
                    object myObject = command.ExecuteScalar();

                    if (!(myObject is DBNull) && null != myObject)
                    {
                        returnValue = myObject.ToString();
                    }
                }
            }
            finally
            {
                finishLogEntry(entry, 0);
            }

            return returnValue;
        }

        protected virtual void runMaintenanceNonQueryLooped(SqlConnection conn, string query, int timeout, int loopMax, decimal waitInSeconds, int batchCount, LogEntry entry)
        {

            entry.Value += "\r\n" + "loopMax:" + loopMax + "\r\n" + "timeout:" + timeout + "\r\n" + "waitInSeconds:" + waitInSeconds + "\r\n" + "batchCount:" + batchCount;

            int records = -1;
            int loop = 0;
            int totalRecords = 0;
            if (null == entry.Items)
            {
                entry.Items = new List<LogEntry>();
            }

            SqlCommand command = null;

            try
            {
                command = new SqlCommand(query);
                command.Connection = conn;
                command.CommandTimeout = timeout;

                while (-1 == records || (batchCount == records && loop < loopMax))
                {
                    entry.Items.Add(createLogEntry(entry.Key + "_" + loop.ToString("000"), "", -1));
                    records = command.ExecuteNonQuery();

                    records = records < 0 ? 0 : records;
                    totalRecords += records;

                    finishLogEntry(entry.Items[entry.Items.Count - 1], records);
                    loop++;
                    this.waitInSeconds(waitInSeconds);
                }
            }
            catch (Exception ex)
            {
                ex.Data.Add("runMaintenanceNonQueryLooped__query", query);
                ex.Data.Add("runMaintenanceNonQueryLooped__records", records);
                ex.Data.Add("runMaintenanceNonQueryLooped__loop", loop);
                ex.Data.Add("runMaintenanceNonQueryLooped__loopMax", loopMax);
                ex.Data.Add("runMaintenanceNonQueryLooped__batchCount", batchCount);

                throw new Exception(ex.Message, ex);
            }
            finally
            {
                finishLogEntry(entry, totalRecords);
            }

        }

        protected virtual void runMaintenanceNonQuery(SqlConnection conn, string query, int timeout, LogEntry entry)
        {

            entry.Value += "\r\n" + "timeout:" + timeout;
            int records = 0;

            try
            {
                using (var command = new SqlCommand(query))
                {
                    command.Connection = conn;
                    command.CommandTimeout = timeout;
                    records = command.ExecuteNonQuery();

                    records = records < 0 ? 0 : records;
                }
            }
            finally
            {
                finishLogEntry(entry, records);
            }

        }

        protected virtual void runMaintenanceStoreProcedure(SqlConnection conn, SqlParameter[] parameters, int timeout, string storeProcedureName, LogEntry entry)
        {
            entry.Value += "\r\n" + "timeout:" + timeout + "\r\n" + "storeProcedureName:" + storeProcedureName;
            int records = 0;

            try
            {
                if (doesStoreProcedure(conn, "", storeProcedureName, timeout))
                {
                    using (var command = new SqlCommand(storeProcedureName))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddRange(parameters);
                        command.Connection = conn;
                        command.CommandTimeout = timeout;
                        records = command.ExecuteNonQuery();
                    }
                }
                else
                {
                    records = -1;
                    entry.Value += "\r\n" + storeProcedureName + "_DOES_NOT_EXIST";
                }
            }
            catch(Exception ex)
            {
                records = -1;
                entry.Value += "\r\n" + ex.Message;
            }
            finally
            {
                finishLogEntry(entry, records);
            }

        }

        protected virtual SqlParameter[] getSqlParameters(NameValueCollection queries, string keyName)
        {
            List<SqlParameter> returnValue = new List<SqlParameter>();
            string[] keys = queries.AllKeys.OrderBy(x => x).ToArray();
            Int16 testValue = 0;
            string newKey = "";
            SqlParameter temp = null;

            foreach (string eachKey in keys)
            {
                newKey = eachKey.ToLower();
                switch (newKey)
                {
                    case QUERY_KEY_MAIN:
                    case QUERY_KEY_DATE:
                    case QUERY_KEY_OVERRIDE_TIMEOUT_TASK:
                    case QUERY_KEY_OVERRIDE_TIMEOUT_URL:
                        break;
                    default:
                        if (newKey.Contains("sql_" + keyName))
                        {
                            newKey = newKey.Replace("sql_" + keyName, "");
                            temp = getSqlParameterByName(returnValue, newKey);
                            if (null != temp)
                            {
                                if (Int16.TryParse(queries[eachKey], out testValue))
                                    temp.Value = testValue;
                            }
                            else
                            {
                                if (Int16.TryParse(queries[eachKey], out testValue))
                                {
                                    returnValue.Add(new SqlParameter("@" + newKey, testValue));
                                }
                                else
                                {
                                    returnValue.Add(new SqlParameter("@" + newKey, queries[eachKey]));
                                }
                            }
                        }
                        break;
                }
            }

            return returnValue.ToArray();
        }

        protected virtual SqlParameter getSqlParameterByName(List<SqlParameter> parameters, string parameterName)
        {

            return parameters.Where(x => parameterName == x.ParameterName).FirstOrDefault();
        }

        protected virtual void updateSqlParameter(List<SqlParameter> parameters, string parameterName, object value)
        {

            var temp = getSqlParameterByName(parameters, parameterName);
            if (null != temp)
                temp.Value = value;

        }

        protected virtual Int16 getShortFromBool(bool value)
        {
            return (Int16)(value ? 1 : 0);
        }

        protected virtual void updateSqlParameter(List<SqlParameter> parameters, string parameterName, bool value)
        {
            updateSqlParameter(parameters, parameterName, getShortFromBool(value));

        }
		
        /// <summary>
        /// Store Front Maintenance: runs store procedure
        /// </summary>
        /// <param name="purgeAnonCustomers">Purge Anon Customers: Default is true</param>
        /// <param name="cleanShoppingCartsOlderThan">Clean Shopping Carts Older Than: Default is 30 days, but set to 0 to disable erasing</param>
        /// <param name="cleanWishListsOlderThan">Clean Wish Lists Older Than: Default is 30 days, but set to 0 to disable erasing</param>
        /// <param name="eraseCCFromAddresses">Erase Credit Card From Addresses: Default is true, but except those used for recurring billing items!</param>
        /// <param name="clearProductViewsOrderThan">Clear Product Views Order Than: Default is 180 days</param>
        /// <param name="eraseCCFromOrdersOlderThan">Erase Credit Cards From Orders Older Than: Default is 30 days, but set to 0 to disable erasing</param>
        /// <param name="defragIndexes">Defrag Indexes: Default is false</param>
        /// <param name="updateStats">Update Stats: Default is false</param>
        /// <param name="purgeDeletedRecords">Purge Deleted Records: Default is false, Purges records in all tables with a deleted flag set to 1</param>
        /// <param name="removeRTShippingDataOlderThan">Remove RT Shipping Data Older Than: Default is 30 days, but set to 0 to disable erasing</param>
        /// <param name="clearSearchLogOlderThan">Clear Search Log Older Than: Default is 30 days, but set to 0 to disable erasing</param>
        /// <param name="cleanOrphanedLocalizedNames">Clean Orphaned Localized Names: Default is false</param>
        /// <param name="cleanupSecurityLog">Cleanup Security Log: Default is false</param>
        /// <param name="clearProfilesOlderThan">Clear Profiles Older Than: Default is 0 Days</param>
        protected virtual List<SqlParameter> getStoreFrontDefaultMaintenanceParameters(bool purgeAnonCustomers = true, Int16 cleanShoppingCartsOlderThan = 30, Int16 cleanWishListsOlderThan = 30, bool eraseCCFromAddresses = true, Int16 clearProductViewsOrderThan = 180, Int16 eraseCCFromOrdersOlderThan = 30, bool defragIndexes = false, bool updateStats = false, bool purgeDeletedRecords = false, Int16 removeRTShippingDataOlderThan = 30, Int16 clearSearchLogOlderThan = 30, bool cleanOrphanedLocalizedNames = false, bool cleanupSecurityLog = false, Int16 clearProfilesOlderThan = 0)
        {
            List<SqlParameter> returnValue = new List<SqlParameter>()
            {
                DB.CreateSQLParameter("@purgeAnonCustomers", SqlDbType.TinyInt, 1, getShortFromBool(purgeAnonCustomers), ParameterDirection.Input),
                DB.CreateSQLParameter("@cleanShoppingCartsOlderThan", SqlDbType.SmallInt, 2, cleanShoppingCartsOlderThan, ParameterDirection.Input),
                DB.CreateSQLParameter("@cleanWishListsOlderThan", SqlDbType.SmallInt, 2, cleanWishListsOlderThan, ParameterDirection.Input),
                DB.CreateSQLParameter("@eraseCCFromAddresses", SqlDbType.TinyInt, 1, getShortFromBool(eraseCCFromAddresses), ParameterDirection.Input),
                DB.CreateSQLParameter("@clearProductViewsOrderThan", SqlDbType.SmallInt, 2, clearProductViewsOrderThan, ParameterDirection.Input),
                DB.CreateSQLParameter("@eraseCCFromOrdersOlderThan", SqlDbType.SmallInt, 2, eraseCCFromOrdersOlderThan, ParameterDirection.Input),
                DB.CreateSQLParameter("@defragIndexes", SqlDbType.TinyInt, 1,getShortFromBool(defragIndexes), ParameterDirection.Input),
                DB.CreateSQLParameter("@updateStats", SqlDbType.TinyInt, 1, getShortFromBool(updateStats), ParameterDirection.Input),
                DB.CreateSQLParameter("@purgeDeletedRecords", SqlDbType.TinyInt, 1, getShortFromBool(purgeDeletedRecords), ParameterDirection.Input),
                DB.CreateSQLParameter("@removeRTShippingDataOlderThan", SqlDbType.SmallInt, 2, removeRTShippingDataOlderThan, ParameterDirection.Input),
                DB.CreateSQLParameter("@clearSearchLogOlderThan", SqlDbType.SmallInt, 2, clearSearchLogOlderThan, ParameterDirection.Input),
                DB.CreateSQLParameter("@cleanOrphanedLocalizedNames", SqlDbType.TinyInt, 2, getShortFromBool(cleanOrphanedLocalizedNames), ParameterDirection.Input),
                DB.CreateSQLParameter("@cleanupSecurityLog", SqlDbType.TinyInt, 2, getShortFromBool(cleanupSecurityLog), ParameterDirection.Input),
                DB.CreateSQLParameter("@clearProfilesOlderThan", SqlDbType.SmallInt, 2,  clearProfilesOlderThan, ParameterDirection.Input),
            };

            var savedSetting = AppLogic.AppConfig("System.SavedMonthlyMaintenance", 0, true);
            if (!string.IsNullOrWhiteSpace(savedSetting))
            {
                string[] token = null;
                string parmName = "";
                string parmValue = "";

                foreach (string eachSetting in savedSetting.Split(','))
                {
                    token = eachSetting.Trim().Split('=');
                    if (2 == token.Length)
                    {
                        parmName = token[0].ToUpper(CultureInfo.InvariantCulture).Trim();
                        parmValue = token[1].ToUpper(CultureInfo.InvariantCulture).Trim();

                        switch (parmName)
                        {
                            case "PURGEANONUSERS":
                                updateSqlParameter(returnValue, "@purgeAnonCustomers", (parmValue == "TRUE"));
                                break;
                            case "PURGEDELETEDRECORDS":
                                updateSqlParameter(returnValue, "@purgeDeletedRecords", (parmValue == "TRUE"));
                                break;
                            case "CLEARALLSHOPPINGCARTS":
                                updateSqlParameter(returnValue, "@cleanShoppingCartsOlderThan", Convert.ToInt16(parmValue));
                                break;
                            case "CLEARALLWISHLISTS":
                                updateSqlParameter(returnValue, "@cleanWishListsOlderThan", Convert.ToInt16(parmValue));
                                break;
                            case "ERASEORDERCREDITCARDS":
                                updateSqlParameter(returnValue, "@eraseCCFromOrdersOlderThan", Convert.ToInt16(parmValue));
                                break;
                            case "ERASEADDRESSCREDITCARDS":
                                updateSqlParameter(returnValue, "@eraseCCFromAddresses", (parmValue == "TRUE"));
                                break;
                            case "CLEARPRODUCTVIEWSOLDERTHAN":
                                updateSqlParameter(returnValue, "@clearProductViewsOrderThan", Convert.ToInt16(parmValue));
                                break;
                            case "CLEANUPLOCALIZATIONDATA":
                                updateSqlParameter(returnValue, "@cleanOrphanedLocalizedNames", (parmValue == "TRUE"));
                                break;
                            case "TUNEINDEXES":
                                updateSqlParameter(returnValue, "@defragIndexes", (parmValue == "TRUE"));
                                break;
                            case "UPDATESTATISTICS":
                                updateSqlParameter(returnValue, "@updateStats", (parmValue == "TRUE"));
                                break;
                            case "CLEARPROFILES":
                                updateSqlParameter(returnValue, "@clearProfilesOlderThan", Convert.ToInt16(parmValue));
                                break;
                            case "CLEARRTSHIPPING":
                                updateSqlParameter(returnValue, "@removeRTShippingDataOlderThan", Convert.ToInt16(parmValue));
                                break;
                            case "CLEARSEARCH":
                                updateSqlParameter(returnValue, "@clearSearchLogOlderThan", Convert.ToInt16(parmValue));
                                break;
                            case "CLEANUPSECURITYLOG":
                                updateSqlParameter(returnValue, "@cleanupSecurityLog", (parmValue == "TRUE"));
                                break;
                            case "INVALIDATEUSERLOGINS":
                                //???
                                break;
                            case "CLEARALLGIFTREGISTRIES":
                                //???
                                break;
                            case "ERASESQLLOG":
                                //???
                                break;
                            case "SAVESETTINGS":
                                //???
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            return returnValue;
        }

        #endregion

        #region QUERY, KEYS, AND VALID

        protected virtual string getUrlQuery(NameValueCollection queries)
        {
            //StringBuilder returnValue = new StringBuilder(QUERY_KEY_DATE + "=" + DateTime.UtcNow.ToString(DATE_FORMAT));
            StringBuilder returnValue = new StringBuilder();

            string[] keys = queries.AllKeys.Where(x => QUERY_KEY_MAIN != x && QUERY_KEY_DATE != x && QUERY_KEY_OVERRIDE_TIMEOUT_TASK != x && QUERY_KEY_OVERRIDE_TIMEOUT_URL != x).OrderBy(x => x).ToArray();

            foreach (var eachKey in keys)
            {
                if (0 == returnValue.Length)
                {
                    returnValue.Append("?");
                }
                else
                {
                    returnValue.Append("&");
                }

                returnValue.Append(eachKey + "=" + queries[eachKey]);
            }

            return returnValue.ToString();
        }

        protected virtual string getEncryptUrlQuery(NameValueCollection queries)
        {
            string returnValue = "";

            HMACSHA1 sha1 = new HMACSHA1();
            sha1.Key = Encoding.ASCII.GetBytes(QUERY_PASSKEY + DateTime.UtcNow.ToString(DATE_FORMAT));

            string url = getUrlQuery(queries);
            returnValue = Convert.ToBase64String(sha1.ComputeHash(System.Text.Encoding.ASCII.GetBytes(url))).Replace("&", "_").Replace("?", "_").Replace(" ", "_").Replace("=", "_").Replace("+", "_").Replace("/", "_");

            return returnValue;
        }

        protected virtual bool containsKey(NameValueCollection queries, string key)
        {
            bool returnValue = false;

            if (null != queries.Get(key))
            {
                returnValue = queries.AllKeys.Contains(key);
            }

            return returnValue;
        }

        protected virtual bool validUrl(NameValueCollection queries, bool ssl)
        {
            bool returnValue = false;

            string[] keys = queries.AllKeys.Where(x => QUERY_KEY_MAIN == x).OrderBy(x => x).ToArray();

            if (containsKey(queries, QUERY_KEY_MAIN))
            {

                if (getEncryptUrlQuery(queries) == getValueDecoded(queries[QUERY_KEY_MAIN]))
                {
                    returnValue = !SSL_NEEDED || (SSL_NEEDED && ssl);
                }
            }

            return returnValue;
        }

        protected virtual string getValueOrDefaultString(NameValueCollection queries, string key, string defaultValue)
        {
            string returnValue = defaultValue;

            if (containsKey(queries, key))
            {
                returnValue = queries[key];
            }

            return returnValue;
        }

        protected virtual int getValueOrDefaultInt(NameValueCollection queries, string key, int defaultValue)
        {
            int returnValue = defaultValue;

            if (containsKey(queries, key))
            {
                string temp = queries[key];
                int.TryParse(temp, out returnValue);
            }

            return returnValue;
        }

        protected virtual decimal getValueOrDefaultDecimal(NameValueCollection queries, string key, decimal defaultValue)
        {
            decimal returnValue = defaultValue;

            if (containsKey(queries, key))
            {
                string temp = queries[key];
                decimal.TryParse(temp, out returnValue);
            }

            return returnValue;
        }

        protected virtual void updateResponse(HttpContext context, int statusCode, LogCollection responseData)
        {

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/xml";

            XmlSerializer serializer = new XmlSerializer(typeof(LogCollection));
            using (TextWriter tw = new StringWriter())
            {
                serializer.Serialize(tw, responseData);
                context.Response.Write(tw.ToString());
            }

        }

        protected virtual string getValueDecoded(string value)
        {
            string returnValue = "";

            if (!string.IsNullOrWhiteSpace(value))
            {
                returnValue = System.Web.HttpUtility.HtmlDecode(value);

            }
            return returnValue;
        }

        protected virtual string getValueEncoded(string value)
        {
            string returnValue = "";

            if (!string.IsNullOrWhiteSpace(value))
            {
                returnValue = System.Web.HttpUtility.HtmlEncode(value);

            }
            return returnValue;
        }

        #endregion

        #region ERROR

        protected virtual string getExceptionToLog(Exception ex, int tabOrder, int maxLineLength)
        {
            //ArgumentNullException, if appropriate, or checking for nulls and throwing NullReferenceException

            string newLine = "|";
            string tab = "~";
            StringBuilder returnValue = new StringBuilder();
            if (null != ex)
            {
                returnValue.Append(getTabOrder(tabOrder, newLine, tab) + "--Exception");
                returnValue.Append(getTabOrder(tabOrder, newLine, tab) + "--Type:"+ tab);
                returnValue.Append(ex.GetType().ToString());

                returnValue.Append(getTabOrder(tabOrder, newLine, tab) + "--Message:" + tab);
                if (!string.IsNullOrEmpty(ex.Message))
                {
                    returnValue.Append(ex.Message.Replace(newLine, getTabOrder(tabOrder + 1, newLine, tab)));
                }
                else
                {
                    returnValue.Append("NULL");
                }

                returnValue.Append(getTabOrder(tabOrder, newLine, tab) + "--Source:" + tab);
                if (!string.IsNullOrEmpty(ex.Source))
                {
                    returnValue.Append(ex.Source.Replace(newLine, getTabOrder(tabOrder + 1, newLine, tab)));
                }
                else
                {
                    returnValue.Append("NULL");
                }

                returnValue.Append(getTabOrder(tabOrder, newLine, tab) + "--StackTrace:" + tab);
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    returnValue.Append(ex.StackTrace.Replace(newLine, getTabOrder(tabOrder + 1, newLine, tab)));
                }
                else
                {
                    returnValue.Append("NULL");
                }

                try
                {

                    System.Diagnostics.StackFrame stackFrame = new System.Diagnostics.StackTrace(ex, true).GetFrame(0);

                    if (null != stackFrame)
                    {
                        returnValue.Append(getTabOrder(tabOrder, newLine, tab) + "--Line Number:" + stackFrame.GetFileLineNumber() + tab);
                        returnValue.Append(getTabOrder(tabOrder, newLine, tab) + "--Column Number:" + stackFrame.GetFileColumnNumber() + tab);
                        returnValue.Append(getTabOrder(tabOrder, newLine, tab) + "--File Name:" + stackFrame.GetFileName());

                        System.Reflection.MethodBase myMethod = stackFrame.GetMethod();
                        returnValue.Append(getTabOrder(tabOrder, newLine, tab) + "--Method Name:" + myMethod.Name + tab);
                    }

                }
                catch (Exception)
                {

                }

                //new System.Diagnostics.StackTrace(ex, true).GetFrame(0).GetFileColumnNumber();

                if (null != ex.Data && 0 != ex.Data.Count)
                {
                    returnValue.Append(getTabOrder(tabOrder, newLine, tab) + "--Data:" + tab);
                    foreach (var eachItem in ex.Data.Keys)
                    {

                        if (!(eachItem is DBNull) && (null != eachItem))
                        {
                            returnValue.Append(getTabOrder(tabOrder + 1, newLine, tab) + "\tKEY " + eachItem.ToString() + ":" + tab);

                            if (!(ex.Data[eachItem] is DBNull) && (null != ex.Data[eachItem]))
                            {
                                returnValue.Append(getLimitedString(getString(ex.Data[eachItem]), tabOrder, eachItem, maxLineLength, newLine,  tab));
                            }

                        }
                    }
                }

                if (null != ex.InnerException)
                {
                    returnValue.Append(getTabOrder(tabOrder, newLine, tab) + "--Inner(");
                    returnValue.Append(getExceptionToLog(ex.InnerException, tabOrder + 1, maxLineLength));
                    returnValue.Append(getTabOrder(tabOrder + 1, newLine, tab) + ")");

                }
            }

            return returnValue.ToString();
        }

        protected virtual string getString(int[] items)
        {
            StringBuilder returnValue = new StringBuilder("{");

            if (null != items)
            {
                foreach (int eachItem in items)
                {

                    if (1 != returnValue.Length)
                    {
                        returnValue.Append(",");
                    }

                    returnValue.Append(eachItem);
                }
            }
            else
            {
                returnValue.Append("NULL");
            }

            returnValue.Append("}");

            return returnValue.ToString();
        }

        protected virtual string getString(List<int> items)
        {
            StringBuilder returnValue = new StringBuilder("{");

            if (null != items)
            {
                foreach (int eachItem in items)
                {

                    if (1 != returnValue.Length)
                    {
                        returnValue.Append(",");
                    }

                    returnValue.Append(eachItem);
                }
            }
            else
            {
                returnValue.Append("NULL");
            }

            returnValue.Append("}");

            return returnValue.ToString();
        }

        protected virtual string getString(double[] items)
        {
            StringBuilder returnValue = new StringBuilder("{");

            if (null != items)
            {
                foreach (double eachItem in items)
                {

                    if (1 != returnValue.Length)
                    {
                        returnValue.Append(",");
                    }

                    returnValue.Append(eachItem);
                }
            }
            else
            {
                returnValue.Append("NULL");
            }

            returnValue.Append("}");

            return returnValue.ToString();
        }

        protected virtual string getString(List<double> items)
        {
            StringBuilder returnValue = new StringBuilder("{");

            if (null != items)
            {
                foreach (double eachItem in items)
                {

                    if (1 != returnValue.Length)
                    {
                        returnValue.Append(",");
                    }
                    returnValue.Append(eachItem);
                }
            }
            else
            {
                returnValue.Append("NULL");
            }

            returnValue.Append("}");

            return returnValue.ToString();
        }

        protected virtual string getString(float[] items)
        {
            StringBuilder returnValue = new StringBuilder("{");

            if (null != items)
            {
                foreach (float eachItem in items)
                {

                    if (1 != returnValue.Length)
                    {
                        returnValue.Append(",");
                    }
                    returnValue.Append(eachItem);
                }
            }
            else
            {
                returnValue.Append("NULL");
            }

            returnValue.Append("}");

            return returnValue.ToString();
        }

        protected virtual string getString(List<float> items)
        {
            StringBuilder returnValue = new StringBuilder("{");

            if (null != items)
            {
                foreach (float eachItem in items)
                {

                    if (1 != returnValue.Length)
                    {
                        returnValue.Append(",");
                    }
                    returnValue.Append(eachItem);
                }
            }
            else
            {
                returnValue.Append("NULL");
            }

            returnValue.Append("}");

            return returnValue.ToString();
        }

        protected virtual string getString(string[] items)
        {
            StringBuilder returnValue = new StringBuilder("{");

            if (null != items)
            {
                foreach (string eachItem in items)
                {

                    if (1 != returnValue.Length)
                    {
                        returnValue.Append(",");
                    }

                    if (!string.IsNullOrEmpty(eachItem))
                    {
                        returnValue.Append(eachItem);
                    }
                    else
                    {
                        returnValue.Append("NULL");
                    }
                }
            }
            else
            {
                returnValue.Append("NULL");
            }

            returnValue.Append("}");

            return returnValue.ToString();
        }

        protected virtual string getString(List<string> items)
        {
            StringBuilder returnValue = new StringBuilder("{");

            if (null != items)
            {
                foreach (string eachItem in items)
                {

                    if (1 != returnValue.Length)
                    {
                        returnValue.Append(",");
                    }

                    if (!string.IsNullOrEmpty(eachItem))
                    {
                        returnValue.Append(eachItem);
                    }
                    else
                    {
                        returnValue.Append("NULL");
                    }
                }
            }
            else
            {
                returnValue.Append("NULL");
            }

            returnValue.Append("}");

            return returnValue.ToString();
        }

        protected virtual string getString(object item)
        {
            StringBuilder returnValue = new StringBuilder();

            if (null != item)
            {
                if (item is int[])
                {
                    returnValue.Append(getString((int[])item));
                }
                else if (item is List<int>)
                {
                    returnValue.Append(getString((List<int>)item));
                }
                else if (item is double[])
                {
                    returnValue.Append(getString((double[])item));
                }
                else if (item is List<double>)
                {
                    returnValue.Append(getString((List<double>)item));
                }
                else if (item is float[])
                {
                    returnValue.Append(getString((float[])item));
                }
                else if (item is List<float>)
                {
                    returnValue.Append(getString((List<float>)item));
                }
                else if (item is string[])
                {
                    returnValue.Append(getString((string[])item));
                }
                else if (item is List<string>)
                {
                    returnValue.Append(getString((List<string>)item));
                }
                else
                {
                    returnValue.Append(item.ToString());
                }
            }
            else
            {
                returnValue.Append("NULL");
            }

            return returnValue.ToString();
        }

        protected virtual string getLimitedString(object value, int tabOrder, object eachItem, int maxLineLength, string newLine, string tab)
        {
            string returnValue = value.ToString();

            if (maxLineLength < returnValue.Length)
            {
                returnValue = returnValue.Substring(0, maxLineLength) + " ...";
            }

            return returnValue.Replace(newLine, getTabOrder(tabOrder + 1, newLine, tab));
        }

        protected virtual string getTabOrder(int tabOrder, string preStuff, string tab)
        {
            StringBuilder returnValue = new StringBuilder(preStuff + tab);

            for (int placement = 0; placement < tabOrder; placement++)
            {
                returnValue.Append(tab);
            }

            return returnValue.ToString();
        }

        #endregion

        #region OTHERS

        protected virtual LogEntry createLogEntry(string key, string value, int result)
        {
            return new LogEntry()
            {
                DateEnd = DateTime.MinValue,
                DateStart = DateTime.UtcNow,
                Items = null,
                Key = key,
                Result = result,
                Value = value,
            };
        }

        protected virtual void finishLogEntry(LogEntry entry, int result)
        {
            entry.DateEnd = DateTime.UtcNow;
            entry.Result = result;
        }

        protected virtual void waitInSeconds(decimal seconds)
        {
            System.Threading.Thread.Sleep((int)(seconds * 1000));
        }

        #endregion

        #endregion

        #endregion

    }

    [Serializable()]
    public class Entry
    {
        public DateTime DateStart { get; set; }

        public DateTime DateEnd { get; set; }

        public string Key { get; set; }

        public int Result { get; set; }

        public string Value { get; set; }

    }

    [Serializable()]
    public class LogEntry : Entry
    {

        public List<LogEntry> Items { get; set; }

    }

    [Serializable()]
    public class LogCollection : LogEntry
    {

        public DateTime ServerTime { get; set; }

        public bool Finished { get; set; }

        public int LogCollectionId { get; set; }

        public string LogTypeName { get; set; }

        public string VerionNumber { get; set; }

    }

}
