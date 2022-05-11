using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SQLite;
using System.Reflection;
using CCKPhone.Bussiness;

namespace CCKPhone.DAL
{
	public class SQLiteUtils : IDisposable
	{
		private SQLiteCommand sql_cmd;

		private SQLiteDataAdapter DB;

		private DataSet DS = new DataSet();

		private DataTable DT = new DataTable();

		private static object LockDb = new object();

		public static string data_folder = ConfigurationSettings.AppSettings["data_folder"] ?? (Assembly.GetEntryAssembly().Location.Substring(0, Assembly.GetEntryAssembly().Location.LastIndexOf('\\')) + "\\DB");

		private string dbconnectionString = $"Data Source={data_folder}\\Data\\Data.db";

		private int IdDanhmuc = 0;

		public DataTable ExecuteQuery(string CommandText)
		{
			try
			{
				lock (LockDb)
				{
					using SQLiteConnection sQLiteConnection = new SQLiteConnection(dbconnectionString);
					sQLiteConnection.Open();
					sql_cmd = sQLiteConnection.CreateCommand();
					DB = new SQLiteDataAdapter(CommandText, sQLiteConnection);
					DS.Reset();
					DB.Fill(DS);
					if (DS.Tables.Count > 0)
					{
						DT = DS.Tables[0];
					}
					sQLiteConnection.Close();
					return DT;
				}
			}
			catch (Exception ex)
			{
				Utils.CCKLog("Query", CommandText);
				Utils.CCKLog("Read Database", ex.Message);
			}
			return null;
		}

		public void Dispose()
		{
			sql_cmd.Dispose();
			DB.Dispose();
			DS.Dispose();
			DT.Dispose();
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}

		public DataTable GetAllGroupByUser(string uid)
		{
			try
			{
				return ExecuteQuery("Select id_group from UserGroups where uid= '" + uid + "'");
			}
			catch
			{
			}
			return new DataTable();
		}

		public void IPLogUpdate(string uid, DateTime date, string IP)
		{
			try
			{
				ExecuteQuery(string.Format("Update Account_IPLog set Count=Count + 1,date='{1}' Where IP ='{0}' and uid ='{2}';", IP, date.ToString("dd-MM-yyyy HH:mm"), uid));
			}
			catch
			{
			}
		}

		public bool CheckTableExists(string name)
		{
			DataTable dataTable = ExecuteQuery($"SELECT name FROM sqlite_master WHERE type='table' AND name='{name}'");
			return dataTable != null && dataTable.Rows.Count > 0;
		}

		public void IPLogInser(string uid, DateTime date, string IP)
		{
			try
			{
				DataTable dataTable = ExecuteQuery($"Select * from Account_IPLog Where IP='{IP}' and uid = '{uid}';");
				if (dataTable != null && dataTable.Rows.Count > 0)
				{
					IPLogUpdate(uid, date, IP);
					return;
				}
				ExecuteQuery(string.Format("Insert Into Account_IPLog (uid,date,IP,Count) values ('{0}','{1}','{2}','{3}');", uid, date.ToString("dd-MM-yyyy HH:mm"), IP, 1));
			}
			catch
			{
				ExecuteQuery("create table if not exists \"Account_IPLog\" (\"ID\"\tINTEGER PRIMARY KEY AUTOINCREMENT,\"date\"\tTEXT NOT NULL,\"uid\"\tINT NOT NULL,\"IP\"\tTEXT NOT NULL,\"Count\"\tINT NOT NULL);");
			}
		}

		public void Article_Log_Insert(string uid, DateTime date, string postId, string fbId)
		{
			try
			{
				ExecuteQuery(string.Format("Insert Into Article_Log (date,uid,newsId,FbId) values ('{1}','{0}','{2}','{3}');", uid, date.ToString("ddMMyyyy"), postId, fbId));
			}
			catch
			{
				ExecuteQuery("DROP TABLE Article_Log; CREATE TABLE \"Article_Log\" (\"ID\"\tINTEGER PRIMARY KEY AUTOINCREMENT,\"date\"\tTEXT NOT NULL,\"uid\"\tINT NOT NULL,\"newsId\"\tINTEGER,\"FbId\"\tTEXT);");
				Article_Log_Insert(uid, date, postId, fbId);
			}
			LastActive(new FBItems
			{
				Uid = uid
			}, "");
		}

		public bool Article_Log_Get(string uid, DateTime date)
		{
			DataTable dataTable = ExecuteQuery(string.Format("Select * from Article_Log Where date='{1}' and uid='{0}'", uid, date.ToString("ddMMyyyy")));
			return dataTable != null && dataTable.Rows.Count > 0;
		}

		public void SetDieAccount(string uid, string msg, string status = "Die")
		{
			ExecuteQuery(string.Format("Update Account Set trangthai='{2}', tuongtacngay='{1}' Where Id='{0}'", uid, DateTime.Now.ToString("dd-MM-yyyy HH:mm"), status));
		}

		public int CreateFolder()
		{
			string temp = string.Format("Reg_{0}", DateTime.Now.ToString("dd-MM-yyyy"));
			return CreateFolder(temp);
		}

		public bool IsTableExists(string tableName)
		{
			DataTable dataTable = ExecuteQuery($"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}';");
			return dataTable != null && dataTable.Rows.Count > 0;
		}

		public int CreateFolder(string temp)
		{
			DataTable dataTable = ExecuteQuery($"Select * from DanhMuc Where tendanhmuc='{temp}'");
			if (dataTable == null || dataTable.Rows.Count == 0)
			{
				DataTable dataTable2 = ExecuteQuery(string.Format("Insert Into DanhMuc(tendanhmuc,sothutu,kieudanhmuc) values ('{0}','{1}',''); Select * from DanhMuc Where tendanhmuc='{0}'", temp, DateTime.Now.Subtract(new DateTime(2019, 11, 1)).TotalSeconds));
				if (dataTable2 != null && dataTable2.Rows.Count > 0)
				{
					return Convert.ToInt32(dataTable2.Rows[0]["id_danhmuc"]);
				}
			}
			return Convert.ToInt32(dataTable.Rows[0]["id_danhmuc"]);
		}

		internal DataRow GetAccountById(string p)
		{
			DataTable dataTable = ExecuteQuery($"Select * from Account Where id='{p}'");
			if (dataTable != null && dataTable.Rows.Count > 0)
			{
				return dataTable.Rows[0];
			}
			return null;
		}

		internal void Update2FA(FBItems item)
		{
			ExecuteQuery(string.Format("Update Account Set privatekey='{1}' Where id='{0}'", item.Uid, item.TwoFA));
		}

		internal void UpdateEmail(FBItems item)
		{
			if (item.PassEmail == null)
			{
				item.PassEmail = "";
			}
			string text = "";
			text = ((!(item.PassEmail != "")) ? string.Format("Update Account Set email='{1}' ,name='{2}', Avatar='{3}' Where id='{0}'; select * from Account Where id='{0}' ", item.Uid, item.Email, item.FirstName.Replace("'", ""), item.Avatar ? 1 : 0) : string.Format("Update Account Set email='{1}' ,name='{2}',passemail='{3}', Avatar='{4}' Where id='{0}'; select * from Account Where id='{0}' ", item.Uid, item.Email, item.FirstName.Replace("'", ""), item.PassEmail, item.Avatar ? 1 : 0));
			ExecuteQuery(text);
		}

		internal void UpdateAvaInfo(string uid, int ava)
		{
			ExecuteQuery(string.Format("Update Account Set Avatar='{1}' Where id='{0}'", uid, ava));
		}

		internal void UpdateInfo(FBItems item)
		{
			ExecuteQuery(string.Format("Update Account Set email='{1}',password='{3}',name='{2}',privatekey='{5}',mobile_phone='{4}',token='{6}',proxy = '{7}', useragent='{8}', passemail='{9}',uidlaybai='{10}', cookies='{11}', brand='{12}' Where id='{0}'", item.Uid, item.Email, item.FirstName.Replace("'", ""), item.Pass, item.Phone, item.TwoFA, item.Token, item.MyProxy.ToString(), item.UserAgent, item.PassEmail, item.UidLayBai, item.Cookie, item.Brand));
		}

		internal void UpdateInfo(string id, string name, string email, string birtyday, string token, string total_count)
		{
			ExecuteQuery(string.Format("Update Account Set email='{1}',name='{2}',token='{3}',birthday='{4}',friend_count='{5}' Where id='{0}'", id, email, name.Replace("'", ""), token, birtyday, total_count));
		}

		internal void UpdateCookiesAndToken(FBItems item)
		{
			ExecuteQuery(string.Format("Update Account Set cookies='{1}',token='{2}' Where id='{0}'", item.Uid, item.Cookie, item.Token));
		}

		internal void LoginStatus(string uid, string msg)
		{
			ExecuteQuery(string.Format("Update Account Set thongbao='{2}', tuongtacngay='{1}' Where id='{0}'", uid, DateTime.Now.ToString("dd-MM-yyyy HH:mm"), msg.Replace("'", "")));
		}

		internal void UpdateIpAddress(string uid, string ip)
		{
			ExecuteQuery(string.Format("Update Account Set lastip='{1}' Where id='{0}'", uid, ip));
			try
			{
				ExecuteQuery(string.Format("Update Account Set lastip='{1}' Where id='{0}'; Insert Into IPAddress(ip,uid,date) values ('{0}','{1}','{2}');", uid, ip, DateTime.Now.ToString("ddMMyyyy")));
			}
			catch
			{
				new SQLiteUtils().ExecuteQuery("CREATE TABLE IPAddress (\"id\" INTEGER PRIMARY KEY AUTOINCREMENT UNIQUE, \"ip\" TEXT,\"uid\" TEXT,\"date\" TEXT);");
				ExecuteQuery(string.Format("Update Account Set lastip='{1}' Where id='{0}'; Insert Into IPAddress(ip,uid,date) values ('{0}','{1}','{2}');", uid, ip, DateTime.Now.ToString("ddMMyyyy")));
			}
		}

		internal void LastActive(FBItems item, string msg)
		{
			ExecuteQuery(string.Format("Update Account Set trangthai='Live', tuongtacngay='{1}', thongbao='{2}' Where id='{0}'", item.Uid, DateTime.Now.ToString("dd-MM-yyyy HH:mm"), msg.Replace("'", "")));
		}

		public void Insert(FBItems item, ProxyInfo proxy, int mucid)
		{
			try
			{
				if (item.Uid == "")
				{
					item.Uid = item.Email;
				}
				DataRow accountById = GetAccountById(item.Uid);
				if (accountById == null)
				{
					string text = item.Brand.ToString();
					if (!text.Contains("\r"))
					{
						text = text.Replace("\n", "\n\r");
					}
					item.Brand = text;
					ExecuteQuery(string.Format("Insert Into Account(id,password, id_danhmuc,name,cookies,privatekey,proxy,trangthai, isprocess,email,birthday,mobile_phone,datecreate,useragent,passemail,uidlaybai,token,tuongtacngay,birthday,brand) values ('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{11}',1,'{7}','{8}','{9}','{10}','{12}','{13}','{14}','{15}','{16}','{17}','{18}')", item.Uid, item.Pass, mucid, item.FirstName.Replace("'", "") + " " + item.LastName.Replace("'", ""), item.Cookie, item.TwoFA, (proxy.Ip != "") ? proxy.ToString() : "", item.Email, item.DateOfBirth, item.Phone, DateTime.Now.ToString("dd-MM-yyyy"), item.TrangThai, (item.UserAgent == "") ? "" : item.UserAgent, item.PassEmail, item.UidLayBai, item.Token, DateTime.Now.ToString("dd-MM-yyyy HH:mm"), item.DateOfBirth, item.Brand.ToString()));
				}
				else
				{
					UpdateInfo(item);
				}
			}
			catch
			{
			}
		}

		public void UpdateFriendCount(string count, string uid)
		{
			int result = 0;
			int.TryParse(count, out result);
			ExecuteQuery(string.Format("Update Account Set friend_count={1} Where id='{0}'", uid, result));
		}

		internal void UpdatePass(string Uid, string strPass)
		{
			ExecuteQuery(string.Format("Update Account Set password='{1}' Where id='{0}'", Uid, strPass));
		}

		internal DataTable GetAccountByDevice(string d)
		{
			return ExecuteQuery($"Select a.id,a.name,a.email,a.device,b.tendanhmuc,a.trangthai,a.brand from Account a inner join danhmuc b on a.id_danhmuc = b.id_danhmuc where a.device  in ('{d}')");
		}

		internal void UpdateCategory(List<string> item, string cat)
		{
			string arg = string.Join(",", item);
			ExecuteQuery(string.Format("Update Account Set id_danhmuc='{1}' Where id in ({0})", arg, cat));
		}

		internal void UpdateProxy(string id, string prox)
		{
			ExecuteQuery($"Update Account Set Proxy='{prox}' Where id='{id}'");
		}

		internal void Insert(FBItems retCC)
		{
			string temp = string.Format("Reg-NVR-{0}", DateTime.Now.ToString("ddMMyyyy"));
			IdDanhmuc = CreateFolder(temp);
			if (IdDanhmuc > 0)
			{
				Insert(retCC, new ProxyInfo(), IdDanhmuc);
			}
		}
	}
}
