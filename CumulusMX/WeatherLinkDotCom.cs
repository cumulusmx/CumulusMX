using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace CumulusMX
{
	internal class WlDotCom
	{
		public static string CalculateApiSignature(string apiSecret, string data)
		{
			/*
			 Calculate the HMAC SHA-256 hash that will be used as the API Signature.
			 */
			string apiSignatureString;
			using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret)))
			{
				byte[] apiSignatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
				apiSignatureString = BitConverter.ToString(apiSignatureBytes).Replace("-", "").ToLower();
			}
			return apiSignatureString;
		}
	}

	public class WlHistory
	{
		public int station_id { get; set; }
		public List<WlHistorySensor> sensors { get; set; }

		public long generated_at { get; set; }
	}

	public class WlHistorySensor
	{
		public int lsid { get; set; }
		public int sensor_type { get; set; }
		public int data_structure_type { get; set; }
		// We have no idea what data structures are going to be in here in advance = dynamic
		public List<string> data { get; set; }
	}


	// Data Structure type 11 = ISS archive record
	public class WlHistorySensorDataType11
	{
		public int tx_id { get; set; }
		public long ts { get; set; }
		public int arch_int { get; set; }
		public double? temp_last { get; set; }
		public double temp_avg { get; set; }
		public double? temp_hi { get; set; }
		public long temp_hi_at { get; set; }
		public double? temp_lo { get; set; }
		public long temp_lo_at { get; set; }
		public double? hum_last { get; set; }
		public double? hum_hi { get; set; }
		public long hum_hi_at { get; set; }
		public double? hum_lo { get; set; }
		public long hum_lo_at { get; set; }
		public double? dew_point_last { get; set; }
		public double? dew_point_hi { get; set; }
		public long dew_point_hi_at { get; set; }
		public double? dew_point_lo { get; set; }
		public long dew_point_lo_at { get; set; }
		public double wet_bulb_last { get; set; }
		public double wet_bulb_hi { get; set; }
		public long wet_bulb_hi_at { get; set; }
		public double wet_bulb_lo { get; set; }
		public long wet_bulb_lo_at { get; set; }
		public double? wind_speed_avg { get; set; }
		public int wind_dir_of_prevail { get; set; }
		public double? wind_speed_hi { get; set; }
		public int? wind_speed_hi_dir { get; set; }
		public long wind_speed_hi_at { get; set; }
		public double? wind_chill_last { get; set; }
		public double? wind_chill_lo { get; set; }
		public long wind_chill_lo_at { get; set; }
		public double heat_index_last { get; set; }
		public double heat_index_hi { get; set; }
		public long heat_index_hi_at { get; set; }
		public double thw_index_last { get; set; }
		public double thw_index_hi { get; set; }
		public long thw_index_hi_at { get; set; }
		public double thw_index_lo { get; set; }
		public long thw_index_lo_at { get; set; }
		public double thsw_index_last { get; set; }
		public double thsw_index_hi { get; set; }
		public long thsw_index_hi_at { get; set; }
		public double thsw_index_lo { get; set; }
		public long thsw_index_lo_at { get; set; }
		public int rain_size { get; set; }
		public int? rainfall_clicks { get; set; }
		public double rainfall_in { get; set; }
		public double rainfall_mm { get; set; }
		public int? rain_rate_hi_clicks { get; set; }
		public double rain_rate_hi_in { get; set; }
		public double rain_rate_hi_mm { get; set; }
		public long rain_rate_hi_at { get; set; }
		public int? solar_rad_avg { get; set; }
		public int solar_rad_hi { get; set; }
		public long solar_rad_hi_at { get; set; }
		public double? et { get; set; }
		public double? uv_index_avg { get; set; }
		public double uv_index_hi { get; set; }
		public long uv_index_hi_at { get; set; }
		public double solar_rad_volt_last { get; set; }
		public double uv_volt_last { get; set; }
		public int reception { get; set; }
		public int rssi { get; set; }
		public int error_packets { get; set; }
		public int resynchs { get; set; }
		public int good_packets_streak { get; set; }
		public uint trans_battery_flag { get; set; }
		public double trans_battery { get; set; }
		public double solar_volt_last { get; set; }
		public double supercap_volt_last { get; set; }
		public int afc { get; set; }
		public double wind_run { get; set; }
		public double solar_energy { get; set; }
		public double uv_dose { get; set; }
		public double cooling_degree_days { get; set; }
		public double heating_degree_days { get; set; }
	}


	// Data structure type 13 = Non-IIS Archive record
	public class WlHistorySensorDataType13
	{
		public int tx_id { get; set; }
		public long ts { get; set; }
		public int arch_int { get; set; }
		public double? temp_last_1 { get; set; }
		public double temp_hi_1 { get; set; }
		public long temp_hi_at_1 { get; set; }
		public double temp_lo_1 { get; set; }
		public long temp_lo_at_1 { get; set; }
		public double? temp_last_2 { get; set; }
		public double temp_hi_2 { get; set; }
		public long temp_hi_at_2 { get; set; }
		public double temp_lo_2 { get; set; }
		public long temp_lo_at_2 { get; set; }
		public double? temp_last_3 { get; set; }
		public double temp_hi_3 { get; set; }
		public long temp_hi_at_3 { get; set; }
		public double temp_lo_3 { get; set; }
		public long temp_lo_at_3 { get; set; }
		public double? temp_last_4 { get; set; }
		public double temp_hi_4 { get; set; }
		public long temp_hi_at_4 { get; set; }
		public double temp_lo_4 { get; set; }
		public long temp_lo_at_4 { get; set; }
		public double? moist_soil_last_1 { get; set; }
		public double moist_soil_hi_1 { get; set; }
		public long moist_soil_hi_at_1 { get; set; }
		public double moist_soil_lo_1 { get; set; }
		public long moist_soil_lo_at_1 { get; set; }
		public double? moist_soil_last_2 { get; set; }
		public double moist_soil_hi_2 { get; set; }
		public int moist_soil_hi_at_2 { get; set; }
		public double moist_soil_lo_2 { get; set; }
		public long moist_soil_lo_at_2 { get; set; }
		public double? moist_soil_last_3 { get; set; }
		public double moist_soil_hi_3 { get; set; }
		public long moist_soil_hi_at_3 { get; set; }
		public double moist_soil_lo_3 { get; set; }
		public long moist_soil_lo_at_3 { get; set; }
		public double? moist_soil_last_4 { get; set; }
		public double moist_soil_hi_4 { get; set; }
		public long moist_soil_hi_at_4 { get; set; }
		public double moist_soil_lo_4 { get; set; }
		public long moist_soil_lo_at_4 { get; set; }
		public double? wet_leaf_last_1 { get; set; }
		public double wet_leaf_hi_1 { get; set; }
		public long wet_leaf_hi_at_1 { get; set; }
		public double wet_leaf_lo_1 { get; set; }
		public long wet_leaf_lo_at_1 { get; set; }
		public double wet_leaf_min_1 { get; set; }
		public double? wet_leaf_last_2 { get; set; }
		public double wet_leaf_hi_2 { get; set; }
		public long wet_leaf_hi_at_2 { get; set; }
		public double wet_leaf_lo_2 { get; set; }
		public long wet_leaf_lo_at_2 { get; set; }
		public double wet_leaf_min_2 { get; set; }

		public object this[string name]
		{
			get
			{
				Type myType = typeof(WlHistorySensorDataType13);
				PropertyInfo myPropInfo = myType.GetProperty(name);
				return myPropInfo.GetValue(this, null);
			}
		}
	}

	public class WlHistorySensorDataType13Baro
	{
		public int arch_int { get; set; }
		public long ts { get; set; }
		public double? bar_sea_level { get; set; }
		public double? bar_hi { get; set; }
		public long bar_hi_at { get; set; }
		public double? bar_lo { get; set; }
		public long bar_lo_at { get; set; }
		public double? bar_absolute { get; set; }

	}

	public class WlHistorySensorDataType13Temp
	{
		public long ts { get; set; }
		public double? temp_in_last { get; set; }
		public double temp_in_hi { get; set; }
		public long temp_in_hi_at { get; set; }
		public double temp_in_lo { get; set; }
		public long temp_in_lo_at { get; set; }
		public double? hum_in_last { get; set; }
		public double hum_in_hi { get; set; }
		public long hum_in_hi_at { get; set; }
		public double hum_in_lo { get; set; }
		public long hum_in_lo_at { get; set; }
		public double dew_point_in { get; set; }
		public double heat_index_in { get; set; }
	}


	// Data structure type 15 = WeatherLink Live Health record
	public class WlHistorySensorDataType15
	{
		public long ts { get; set; }
		public int health_version { get; set; }
		public long firmware_version { get; set; }
		public long bluetooth_version { get; set; }
		public long radio_version { get; set; }
		public long espressif_version { get; set; }
		public int battery_voltage { get; set; }
		public int input_voltage { get; set; }
		public double uptime { get; set; }
		public int bgn { get; set; }
		public int network_type { get; set; }
		public int ip_address_type { get; set; }
		public string ip_v4_address { get; set; }
		public string ip_v4_gateway { get; set; }
		public string ip_v4_netmask { get; set; }
		public int dns_type_used { get; set; }
		public long rx_bytes { get; set; }
		public long tx_bytes { get; set; }
		public long local_api_queries { get; set; }
		public long rapid_records_sent { get; set; }
		public int? wifi_rssi { get; set; }
		public double link_uptime { get; set; }
		public int network_error { get; set; }
		public int touchpad_wakeups { get; set; }
		public long bootloader_version { get; set; }
	}


	// Data structure type 17 = AirLink Archive record
	public class WlHistorySensorDataType17
	{
		public long ts { get; set; }
		public int arch_int { get; set; }
		public double temp_avg { get; set; }
		public double temp_hi { get; set; }
		public long temp_hi_at { get; set; }
		public double temp_lo { get; set; }
		public long temp_lo_at { get; set; }
		public double hum_last { get; set; }
		public double hum_hi { get; set; }
		public long hum_hi_at { get; set; }
		public double hum_lo { get; set; }
		public long hum_lo_at { get; set; }
		public double dew_point_last { get; set; }
		public double dew_point_hi { get; set; }
		public long dew_point_hi_at { get; set; }
		public double dew_point_lo { get; set; }
		public long dew_point_lo_at { get; set; }
		public double wet_bulb_last { get; set; }
		public double wet_bulb_hi { get; set; }
		public long wet_bulb_hi_at { get; set; }
		public double wet_bulb_lo { get; set; }
		public long wet_bulb_lo_at { get; set; }
		public double heat_index_last { get; set; }
		public double heat_index_hi { get; set; }
		public long heat_index_hi_at { get; set; }
		public double pm_1_avg { get; set; }
		public double pm_1_hi { get; set; }
		public long pm_1_hi_at { get; set; }
		public double pm_2p5_avg { get; set; }
		public double pm_2p5_hi { get; set; }
		public int pm_2p5_hi_at { get; set; }
		public double pm_10_avg { get; set; }
		public double pm_10_hi { get; set; }
		public long pm_10_hi_at { get; set; }
		public double pm_0p3_avg_num_part { get; set; }
		public double pm_0p3_hi_num_part { get; set; }
		public double pm_0p5_avg_num_part { get; set; }
		public double pm_0p5_hi_num_part { get; set; }
		public double pm_1_avg_num_part { get; set; }
		public double pm_1_hi_num_part { get; set; }
		public double pm_2p5_avg_num_part { get; set; }
		public double pm_2p5_hi_num_part { get; set; }
		public double pm_5_avg_num_part { get; set; }
		public double pm_5_hi_num_part { get; set; }
		public double pm_10_avg_num_part { get; set; }
		public double pm_10_hi_num_part { get; set; }
		public string aqi_type { get; set; }
		public double aqi_avg_val { get; set; }
		public string aqi_avg_desc { get; set; }
		public double aqi_hi_val { get; set; }
		public string aqi_hi_desc { get; set; }
	}

	// Data Structure type 18 = AirLink Health record
	public class WlHistorySensorDataType18
	{
		public long? air_quality_firmware_version { get; set; } // OLD original incorrect name
		public long? firmware_version { get; set; }             // NEW correct name
		public string application_sha { get; set; }
		public string application_version { get; set; }
		public long bootloader_version { get; set; }
		public int? dns_type_used { get; set; }
		public int dropped_packets { get; set; }
		public int health_version { get; set; }
		public int internal_free_mem_chunk_size { get; set; }
		public int internal_free_mem_watermark { get; set; }
		public int internal_free_mem { get; set; }
		public int internal_used_mem { get; set; }
		public int ip_address_type { get; set; }
		public string ip_v4_address { get; set; }
		public string ip_v4_gateway { get; set; }
		public string ip_v4_netmask { get; set; }
		public long link_uptime { get; set; }
		public int local_api_queries { get; set; }
		public int? network_error { get; set; }
		public int packet_errors { get; set; }
		public int record_backlog_count { get; set; }
		public int record_stored_count { get; set; }
		public int record_write_count { get; set; }
		public long rx_packets { get; set; }
		public int total_free_mem { get; set; }
		public int total_used_mem { get; set; }
		public long ts { get; set; }
		public long tx_packets { get; set; }
		public double uptime { get; set; }
		public int? wifi_rssi { get; set; }
	}

	public class WlErrorResponse
	{
		public int code { get; set; }
		public string message { get; set; }
	}

	public class WlSensorList
	{
		public List<WlSensorListSensor> sensors { get; set; }
		public long generated_at { get; set; }
	}

	public class WlSensorListSensor
	{
		public int lsid { get; set; }
		public int sensor_type { get; set; }
		public string category { get; set; }
		public string manufacturer { get; set; }
		public string product_name { get; set; }
		public string product_number { get; set; }
		public int rain_collector_type { get; set; }
		public bool active { get; set; }
		public long created_date { get; set; }
		public long modified_date { get; set; }
		public int station_id { get; set; }
		public string station_name { get; set; }
		public string parent_device_type { get; set; }
		public string parent_device_name { get; set; }
		public int parent_device_id { get; set; }
		public string parent_device_id_hex { get; set; }
		public int port_number { get; set; }
		public double latitude { get; set; }
		public double longitude { get; set; }
		public double elevation { get; set; }
		public int? tx_id { get; set; }
	}

	public class WlStationList
	{
		public List<WlStationListStations> stations { get; set; }
	}

	public class WlStationListStations
	{
		public int station_id { get; set; }
		public string station_name { get; set; }
		public int gateway_id { get; set; }
		public string gateway_id_hex { get; set; }
		public string product_number { get; set; }
		public string username { get; set; }
		public string user_email { get; set; }
		public string company_name { get; set; }
		public bool active { get; set; }
		//[JsonProperty("private")]
		public bool @private { get; set; } // "private" is a reserved word!
		public int recording_interval { get; set; }
		// firmware_version - always null?
		public long registered_date { get; set; }
		public string time_zone { get; set; }
		public string city { get; set; }
		public string region { get; set; }
		public string country { get; set; }
		public double latitude { get; set; }
		public double longitude { get; set; }
		public double elevation { get; set; }
	}

	public class WlSensor
	{
		public WlSensor(int sensorType, int lsid, int parentId, string name, string parentName)
		{
			SensorType = sensorType;
			LSID = lsid;
			ParentID = parentId;
			Name = name;
			ParentName = parentName;
		}
		public int SensorType { get; set; }
		public int LSID { get; set; }
		public int ParentID { get; set; }
		public string Name { get; set; }
		public string ParentName { get; set; }
	}


	// WeatherLink.com status
	public class WlComSystemStatus
	{
		public WlComSystemStatusResult result {get; set;}
	}

	public class WlComSystemStatusResult
	{
		public WlComStatusOverall status_overall { get; set; }
		public WlComStatus[] status { get; set; }
	}

	public class WlComStatusOverall
	{
		public DateTime updated { get; set; }
		public string status { get; set; }
		public int status_code { get; set; }
	}

	public class WlComStatus : WlComStatusContainer
	{
		public WlComStatusContainer[] containers { get; set; }
	}

	public class WlComStatusContainer : WlComStatusOverall
	{
		public string id { get; set; }
		public string name { get; set; }
	}

}
