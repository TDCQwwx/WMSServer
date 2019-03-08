using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public class MessageSendToDataBase
    {
        private static readonly WMSDbContext WMSDbContext = new WMSDbContext();
        /// <summary>
        /// 初始化数据库——首次使用
        /// </summary>
        /// <returns></returns>
        public static bool InitializeTable()
        {
            StorageInfo info = new StorageInfo()
            {
                RFIDNumber = "00000000",//不存在料盘时的RFID号
                Type = "null",     //null代表空库位
                Number = 0,         //数量为0
                TrayIsExist = false//不存在料盘
            };
            try
            {
                using (var wareHouseDB = new WMSDbContext())
                {
                    for (int index = 0; index < 60; index++)
                    {
                        wareHouseDB.StorageInfo.Add(info);
                        wareHouseDB.SaveChanges();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        /// <summary>
        /// 一般不用
        /// </summary>
        public static void Remove()
        {
            using (var wareHouseDB = new WMSDbContext())
            {
                for (int index = 61; index < 300; index++)
                {
                    StorageInfo storageInfo = wareHouseDB.StorageInfo.Where(p => p.LocationID == index).FirstOrDefault();
                    wareHouseDB.StorageInfo.Remove(storageInfo);

                    wareHouseDB.SaveChanges();
                }
            }
        }

        /// <summary>
        /// 查询所有库位信息
        /// </summary>
        /// <returns></returns>
        public static List<StorageInfo> GetStoragesInfo()
        {
            try
            {
                var searchResultStorageInfoList = WMSDbContext.StorageInfo.ToList();
                return searchResultStorageInfoList;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        /// <summary>
        /// 查询单个库位信息
        /// </summary>
        /// <returns></returns>
        public static StorageInfo GetStorageInfo(int location)
        {
            try
            {
                var storageInfo = WMSDbContext.StorageInfo.Where(p => p.LocationID == location).FirstOrDefault();

                return storageInfo;
            }
            catch (Exception)
            {

                return null;
            }
        }
        /// <summary>
        /// 查询入库记录
        /// </summary>
        /// <returns></returns>
        public static List<InputRecord> GetInputRecords()
        {
            try
            {
                //var searchResultInputRecordList = WMSDbContext.InputRecord.ToList();
                var searchResultInputRecordList = from p in WMSDbContext.InputRecord
                                                  orderby p.InputTime descending
                                                  select p;
                return searchResultInputRecordList.ToList();
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        /// <summary>
        /// 查询出库记录
        /// </summary>
        /// <returns></returns>
        public static List<OutputRecord> GetOutputRecords()
        {
            try
            {
                //var searchResultOutputRecordList = WMSDbContext.OutputRecord.ToList();
                var searchResultOutputRecordList = from p in WMSDbContext.OutputRecord
                                                   orderby p.OutputTime descending
                                                   select p;
                return searchResultOutputRecordList.ToList();
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        /// <summary>
        /// 查询库位修改记录
        /// </summary>
        /// <returns></returns>
        public static List<ModifyStorageRecord> GetModifyStorageRecords()
        {
            try
            {
                //var searchResultModifyStorageRecordList = WMSDbContext.ModifyStorageRecord.ToList();
                var searchResultModifyStorageRecordList = from p in WMSDbContext.ModifyStorageRecord
                                                          orderby p.ModifyTime descending
                                                          select p;
                //return searchResultModifyStorageRecordList.Take<ModifyStorageRecord>(5).ToList();
                return searchResultModifyStorageRecordList.ToList();
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        /// <summary>
        /// 更新库位信息——入库
        /// </summary>
        /// <param name="rfidNumber"></param>
        /// <returns></returns>
        public static bool UpdateInputStoragesInfo(string rfidNumber)
        {
            try
            {
                int location = Convert.ToInt32(rfidNumber.Substring(0, 4), 16);
                var searchResult = WMSDbContext.StorageInfo.Where(p => p.LocationID == location).FirstOrDefault();
                if (searchResult != null)
                {
                    switch (rfidNumber.Substring(4))
                    {
                        case "0000": searchResult.Type = "empty"; searchResult.Number = 0; break;
                        case "0101": searchResult.Type = "bottle"; searchResult.Number = 4; break;
                        case "0201": searchResult.Type = "lid"; searchResult.Number = 4; break;
                        default: break;
                    }
                    searchResult.RFIDNumber = rfidNumber.ToUpper();
                    searchResult.TrayIsExist = true;

                    WMSDbContext.Entry<StorageInfo>(searchResult).State = EntityState.Modified;

                    WMSDbContext.SaveChanges();

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        /// <summary>
        /// 更新库位信息——出库
        /// </summary>
        /// <param name="outputLocation"></param>
        /// <returns></returns>
        public static bool UpdateOutputStoragesInfo(int outputLocation)
        {
            try
            {
                var searchResult = WMSDbContext.StorageInfo.Where(p => p.LocationID == outputLocation).FirstOrDefault();
                if (searchResult != null)
                {
                    searchResult.RFIDNumber = "00000000";
                    searchResult.Type = "null";
                    searchResult.Number = 0;
                    searchResult.TrayIsExist = false;

                    WMSDbContext.Entry<StorageInfo>(searchResult).State = EntityState.Modified;

                    WMSDbContext.SaveChanges();

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
        /// <summary>
        /// 更新库位信息——修改库位
        /// </summary>
        /// <param name="rfidNumber"></param>
        /// <returns></returns>
        public static bool UpdateModifyStoragesInfo(int modifyType, int location,string rfidNumber)
        {
            try
            {
                //int location = Convert.ToInt32(rfidNumber.Substring(0, 4), 16);
                var searchResult = WMSDbContext.StorageInfo.Where(p => p.LocationID == location).FirstOrDefault();
                if (searchResult != null)
                {
                    if (modifyType == 1)//删除盘子
                    {
                        searchResult.RFIDNumber = "00000000";
                        searchResult.Type = "null";
                        searchResult.Number = 0;
                        searchResult.TrayIsExist = false;
                    }
                    if (modifyType == 2)//修改物料种类
                    {
                        switch (rfidNumber.Substring(4))
                        {
                            case "0000": searchResult.Type = "empty"; searchResult.Number = 0;break;
                            case "0101": searchResult.Type = "bottle"; searchResult.Number = 4; break;
                            case "0201": searchResult.Type = "lid"; searchResult.Number = 4; break;
                            default: break;
                        }
                        searchResult.RFIDNumber = rfidNumber.ToUpper();
                        searchResult.TrayIsExist = true;
                    }

                    WMSDbContext.Entry<StorageInfo>(searchResult).State = EntityState.Modified;

                    WMSDbContext.SaveChanges();

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        /// <summary>
        /// 增加一条入库记录
        /// </summary>
        /// <param name="rfidNumber"></param>
        /// <returns></returns>
        public static bool AddInputRecord(string rfidNumber)
        {
            int location = Convert.ToInt32(rfidNumber.Substring(0, 4), 16);
            string category = "";
            switch (rfidNumber.Substring(4))
            {
                case "0000": category = "empty";  break;
                case "0101": category = "bottle"; break;
                case "0201": category = "lid";  break;
                default: break;
            }
            try
            {
                InputRecord inputRecord = new InputRecord
                {
                    Location = location,
                    RFIDNumber = rfidNumber,
                    Category = category,
                    Amount = 4,
                    InputTime = DateTime.Now
                };

                WMSDbContext.InputRecord.Add(inputRecord);

                WMSDbContext.SaveChanges();

                return true;
            }
            catch (Exception)
            {

                return false;
            }

        }
        /// <summary>
        /// 增加一条出库记录
        /// </summary>
        /// <param name="rfidNumber"></param>
        /// <returns></returns>
        public static bool AddOutputRecord(int location)
        {
            StorageInfo storageInfo = GetStorageInfo(location);
            try
            {
                OutputRecord outputRecord = new OutputRecord
                {
                    Location = location,
                    RFIDNumber = storageInfo.RFIDNumber,
                    Category = storageInfo.Type,
                    Amount = 4,
                    OutputTime = DateTime.Now
                };
                WMSDbContext.OutputRecord.Add(outputRecord);

                WMSDbContext.SaveChanges();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        /// <summary>
        /// 增加一条库位修改记录
        /// </summary>
        /// <param name="modifyType"></param>
        /// <param name="rfidNumber"></param>
        /// <returns></returns>
        public static bool AddModifyStorageRecord(int modifyType,int location,string rfidNumber)
        {
            //int location = Convert.ToInt32(rfidNumber.Substring(0, 4), 16);
            string category = "";
            int amount = 0;

            if (modifyType == 1)//删除盘子
            {
                category = "null";
                amount = 0;
            }
            if (modifyType == 2)//修改物料种类
            {
                switch (rfidNumber.Substring(4))
                {
                    case "0000": category = "empty"; amount = 0; break;
                    case "0101": category = "bottle"; amount = 4; break;
                    case "0201": category = "lid"; amount = 4; break;
                    default: break;
                }
            }
            
            try
            {
                ModifyStorageRecord modifyStorageRecord = new ModifyStorageRecord
                {
                    ModifyType = modifyType,
                    Location = location,
                    RFIDNumber = rfidNumber.ToUpper(),
                    Category = category,
                    Amount = amount,
                    ModifyTime = DateTime.Now
                };

                WMSDbContext.ModifyStorageRecord.Add(modifyStorageRecord);

                WMSDbContext.SaveChanges();

                return true;
            }
            catch (Exception)
            {

                return false;
            }
        }
    }
    public class WMSDbContext : DbContext
    {
        public WMSDbContext() : base("name=WMSDB")
        {
        }
        public DbSet<StorageInfo> StorageInfo { get; set; }
        public DbSet<InputRecord> InputRecord { get; set; }
        public DbSet<OutputRecord> OutputRecord { get; set; }
        public DbSet<ModifyStorageRecord> ModifyStorageRecord { get; set; }
    }

    #region Model
    public class StorageInfo
    {
        [Key]
        [Required]
        public int LocationID { get; set; }//库位号：1-60
        [Required]
        public string RFIDNumber { get; set; }//料盘RFID号
        [Required]
        [StringLength(10)]
        public string Type { get; set; }//物料类型："bottle"：瓶子;"lid"：盖子;"empty"：空料盘；"null"：空库位
        [Required]
        public int Number { get; set; }//物料数量：4||0
        [Required]
        public bool TrayIsExist { get; set; }//料盘是否存在
    }
    public class InputRecord
    {
        [Key]
        [Required]
        public int InputRecordID { get; set; }
        [Required]
        public int Location { get; set; }
        [Required]
        public string RFIDNumber { get; set; }
        [Required]
        public string Category { get; set; }
        [Required]
        public int Amount { get; set; }
        [Required]
        public DateTime InputTime { get; set; }
    }
    public class OutputRecord
    {
        [Key]
        [Required]
        public int OutputRecordID { get; set; }
        [Required]
        public int Location { get; set; }
        [Required]
        public string RFIDNumber { get; set; }
        [Required]
        public string Category { get; set; }
        [Required]
        public int Amount { get; set; }
        [Required]
        public DateTime OutputTime { get; set; }
    }
    public class ModifyStorageRecord
    {
        [Key]
        [Required]
        public int ModifyStorageRecordID { get; set; }
        [Required]
        public int ModifyType { get; set; }
        [Required]
        public int Location { get; set; }
        [Required]
        public string RFIDNumber { get; set; }
        [Required]
        public string Category { get; set; }
        [Required]
        public int Amount { get; set; }
        [Required]
        public DateTime ModifyTime { get; set; }
    }
    #endregion
}
