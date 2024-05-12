using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CumulusMX
{
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


	// Data Structure type 3 & 4 - VP2 ISS archive record Type A & B
	public class WlHistorySensorDataType3_4
	{
		public int tx_id { get; set; }
		public long ts { get; set; }
		public int arch_int { get; set; }
		public int rev_type { get; set; }

		public double? temp_out { get; set; }
		public double? temp_out_hi { get; set; }
		public double? temp_out_lo { get; set; }
		public double? temp_in { get; set; }
		public int? hum_in { get; set; }
		public int? hum_out { get; set; }
		public int? rainfall_clicks { get; set; }
		public double? rainfall_in { get; set; }
		public double? rainfall_mm { get; set; }
		public int? rain_rate_hi_clicks { get; set; }
		public double? rain_rate_hi_in { get; set; }
		public double? rain_rate_hi_mm { get; set; }
		public double? et { get; set; }
		public double? bar { get; set; }
		public int? solar_rad_avg { get; set; }
		public int? solar_rad_hi { get; set; }
		public double? uv_index_avg { get; set; }
		public double? uv_index_hi { get; set; }
		public int? wind_num_samples { get; set; }
		public int? wind_speed_avg { get; set; }
		public int? wind_speed_hi { get; set; }
		public int? wind_dir_of_hi { get; set; } // direction code: 0=N, 1=NNE, ... 14=NW, 15=NNW
		public int? wind_dir_of_prevail { get; set; } // direction code: 0=N, 1=NNE, ... 14=NW, 15=NNW
		public int? moist_soil_1 { get; set; }
		public int? moist_soil_2 { get; set; }
		public int? moist_soil_3 { get; set; }
		public int? moist_soil_4 { get; set; }
		public double? temp_soil_1 { get; set; }
		public double? temp_soil_2 { get; set; }
		public double? temp_soil_3 { get; set; }
		public double? temp_soil_4 { get; set; }
		public int? wet_leaf_1 { get; set; }
		public int? wet_leaf_2 { get; set; }
		public double? temp_extra_1 { get; set; }
		public double? temp_extra_2 { get; set; }
		public double? temp_extra_3 { get; set; }
		public double? temp_extra_4 { get; set; }
		public int? hum_extra_1 { get; set; }
		public int? hum_extra_2 { get; set; }
		public int? hum_extra_3 { get; set; }
		public int? hum_extra_4 { get; set; }
		public int? forecast_rule { get; set; }
		public string forecast_desc { get; set; }
		public double? abs_press { get; set; }
		public double? bar_noaa { get; set; }
		public double? bar_alt { get; set; }
		public double? air_density { get; set; }
		public double? dew_point_out { get; set; }
		public double? dew_point_in { get; set; }
		public double? emc { get; set; }
		public double? heat_index_out { get; set; }
		public double? heat_index_in { get; set; }
		public double? wind_chill { get; set; }
		public double? wind_run { get; set; }
		public double? deg_days_heat { get; set; }
		public double? deg_days_cool { get; set; }
		public double? solar_energy { get; set; }
		public double? uv_dose { get; set; }
		public double? thw_index { get; set; }
		public double? thsw_index { get; set; }
		public double? wet_bulb { get; set; }
		public double? night_cloud_cover { get; set; }
		public double? iss_reception { get; set; }

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

	// Data Structure type 11 = WeatherLink Live ISS archive record
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

	// Data structure type 13 = WeatherLink Live Non-IIS Archive record
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

	// Data structure type 13 = WeatherLink Live Internal Barometer Archive record
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

	// Data structure type 13 = WeatherLink Live Internal Temperature Archive record
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

	// Data Structure type 24 = WeatherLink Console ISS Archive record
	public class WlHistorySensorDataType24 : WlHistorySensorDataType11
	{
		public double hdd { get; set; }
		public double cdd { get; set; }
		public int crc_errors { get; set; }
		public int resyncs { get; set; }
		public int packets_received_streak { get; set; }
		public int packets_missed_streak { get; set; }
		public int packets_received { get; set; }
		public int packets_missed { get; set; }
		public int freq_error_avg { get; set; }
		public int freq_error_total { get; set; }
		public double trans_battery_volt { get; set; }
		public double spars_volt_last { get; set; }
		public int spars_rpm_last { get; set; }
		public double latitude { get; set; }
		public double longitude { get; set; }
		public double elevation { get; set; }
		public double gnss_clock { get; set; }
		public double gnss_fix { get; set; }
	}

	// Data Strucrure Type 26 = WeatherLink Console Non-ISS Archive record
	public class WlHistorySensorDataType26 : WlHistorySensorDataType13
	{
		public double temp_last_volt_1 { get; set; }
		public double temp_last_volt_2 { get; set; }
		public double temp_last_volt_3 { get; set; }
		public double temp_last_volt_4 { get; set; }
		public double moist_soil_last_volt_1 { get; set; }
		public double moist_soil_last_volt_2 { get; set; }
		public double moist_soil_last_volt_3 { get; set; }
		public double moist_soil_last_volt_4 { get; set; }
		public int reception { get; set; }
		public int rssi { get; set; }
		public int crc_errors { get; set; }
		public int resyncs { get; set; }
		public int packets_received_streak { get; set; }
		public int packets_missed_streak { get; set; }
		public int packets_received { get; set; }
		public int packets_missed { get; set; }
		public int freq_error_avg { get; set; }
		public int freq_error_total { get; set; }
		public int trans_battery_flag { get; set; }
	}




	// Data Structure Type 11 = ISS Health record
	// Data Structure Type 13 = Non-ISS Health Record
	public class WlHealthDataType11_13
	{
		public int afc { get; set; }
		public int arch_int { get; set; }
		public int error_packets { get; set; }
		public double? et { get; set; }
		public int good_packets_streak { get; set; }
		public int reception { get; set; }
		public int resynchs { get; set; }
		public double? solar_volt_last { get; set; }
		public double? supercap_volt_last { get; set; }
		public int rssi { get; set; }
		public int trans_battery_flag { get; set; }
		public long ts { get; set; }
		public int tx_id { get; set; }
	}

	// Data Structure Type 15 = WeatherLink Live Health record
	public class WlHealthDataType15
	{
		public long ts { get; set; }
		public int health_version { get; set; }
		public long firmware_version { get; set; }
		public long bluetooth_version { get; set; }
		public long radio_version { get; set; }
		public long espressif_version { get; set; }
		public int battery_voltage { get; set; }
		public int input_voltage { get; set; }
		public int uptime { get; set; }
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

	// Data Structure Type 27 = WeatherLink Console Health record
	public class WlHealthDataType27
	{
		public int? health_version { get; set; }
		public string console_sw_version { get; set; }
		public string console_radio_version { get; set; }
		public long? console_api_level { get; set; }
		public int? battery_voltage { get; set; }
		public int? battery_percent { get; set; }
		public int? battery_condition { get; set; }
		public double? battery_current { get; set; }
		public double? battery_temp { get; set; }
		public int? charger_plugged { get; set; }
		public int? battery_status { get; set; }
		public int? os_uptime { get; set; }
		public int? app_uptime { get; set; }
		public int? bgn { get; set; }
		public int? ip_address_type { get; set; }
		public string ip_v4_address { get; set; }
		public string ip_v4_gateway { get; set; }
		public string ip_v4_mask { get; set; }
		public int? dns_type_used { get; set; }
		public long? rx_kilobytes { get; set; }
		public long? tx_kilobytes { get; set; }
		public long? local_api_queries { get; set; }
		public int? wifi_rssi { get; set; }
		public long? link_uptime { get; set; }
		public long? connection_uptime { get; set; }
		public long? bootloader_version { get; set; }
		public int? clock_source { get; set; }
		public int? gnss_sip_tx_id { get; set; }
		public double? free_mem { get; set; }
		public double? internal_free_space { get; set; }
		public double? system_free_space { get; set; }
		public double? queue_kilobytes { get; set; }
		public double? database_kilobytes { get; set; }
		public double? battery_cycle_count { get; set; }
		public string console_os_version { get; set; }
		public long ts { get; set; }
	}





	public class WlCurrent
	{
		public int station_id { get; set; }
		public long generated_at { get; set; }

		// We have no idea what data structures are going to be in here in advance = dynamic
		public List<WlCurrentSensor> sensors { get; set; }
	}

	public class WlCurrentSensor
	{
		public int lsid { get; set; }
		public int sensor_type { get; set; }
		public int? data_structure_type { get; set; }

		// We have no idea what data structures are going to be in here in advance
		public string data { get; set; }
	}



	// Data Structure 1 (rev A) = VP2 ISS current record
	// Data Structure 1 (rev B) = VP2 ISS current record (rev b just adds bar_trend which we do not use)
	public class WLCurrentSensordDataType1_2
	{
		public long ts { get; set; }
		public int bar_trend { get; set; }
		public double? bar { get; set; }
		public double? temp_in { get; set; }
		public int? hum_in { get; set; }
		public double? temp_out { get; set; }
		public int? wind_speed { get; set; }
		public int? wind_dir { get; set; }
		public int? wind_speed_10_min_avg { get; set; }
		public int? wind_gust_10_min { get; set; }
		public int? temp_extra_1 { get; set; }
		public int? temp_extra_2 { get; set; }
		public int? temp_extra_3 { get; set; }
		public int? temp_extra_4 { get; set; }
		public int? temp_extra_5 { get; set; }
		public int? temp_extra_6 { get; set; }
		public int? temp_extra_7 { get; set; }
		public int? temp_soil_1 { get; set; }
		public int? temp_soil_2 { get; set; }
		public int? temp_soil_3 { get; set; }
		public int? temp_soil_4 { get; set; }
		//public int temp_leaf_1 { get; set; }
		//public int temp_leaf_2 { get; set; }
		//public int temp_leaf_3 { get; set; }
		//public int temp_leaf_4 { get; set; }
		public int? hum_out { get; set; }
		public int? hum_extra_1 { get; set; }
		public int? hum_extra_2 { get; set; }
		public int? hum_extra_3 { get; set; }
		public int? hum_extra_4 { get; set; }
		public int? hum_extra_5 { get; set; }
		public int? hum_extra_6 { get; set; }
		public int? hum_extra_7 { get; set; }
		public int? rain_rate_clicks { get; set; }
		public double? rain_rate_in { get; set; }
		public double? rain_rate_mm { get; set; }
		public int? rain_storm_clicks { get; set; }
		public double? rain_storm_in { get; set; }
		public double? rain_storm_mm { get; set; }
		public int? rain_storm_start_date { get; set; }
		public int? rain_day_clicks { get; set; }
		public double? rain_day_in { get; set; }
		public double? rain_day_mm { get; set; }
		public int? rain_month_clicks { get; set; }
		public double? rain_month_in { get; set; }
		public double? rain_month_mm { get; set; }
		public int? rain_year_clicks { get; set; }
		public double? rain_year_in { get; set; }
		public double? rain_year_mm { get; set; }
		public double? uv { get; set; }
		public int? solar_rad { get; set; }
		public double? et_day { get; set; }
		public double? et_month { get; set; }
		public double? et_year { get; set; }
		public int? moist_soil_1 { get; set; }
		public int? moist_soil_2 { get; set; }
		public int? moist_soil_3 { get; set; }
		public int? moist_soil_4 { get; set; }
		public int? wet_leaf_1 { get; set; }
		public int? wet_leaf_2 { get; set; }
		public int? wet_leaf_3 { get; set; }
		public int? wet_leaf_4 { get; set; }
		public int? forecast_rule { get; set; }
		public string forecast_desc { get; set; }
		public double? dew_point { get; set; }
		public double? heat_index { get; set; }
		public double? wind_chill { get; set; }
	}

	// TODO?
	// Add strucure type 5 = VP2 High/Low records

	// Data Structure 10 = WeatherLink Live ISS current record
	// Data Structure 23 = WeatherLink Console current record (additions to type 10 noted below)
	public class WLCurrentSensorDataType10_23
	{
		public int tx_id { get; set; }
		public double? temp { get; set; }
		public double? hum { get; set; }
		public double? dew_point { get; set; }
		public double wet_bulb { get; set; }
		public double? heat_index { get; set; }
		public double? wind_chill { get; set; }
		public double? thw_index { get; set; }
		public double? thsw_index { get; set; }
		public double? wbgt { get; set; } // Type 23 only
		public double? wind_speed_last { get; set; }
		public int? wind_dir_last { get; set; }
		public double? wind_speed_avg_last_1_min { get; set; }
		public int? wind_dir_scalar_avg_last_1_min { get; set; }
		public double? wind_speed_avg_last_2_min { get; set; }
		public int wind_dir_scalar_avg_last_2_min { get; set; }
		public double? wind_speed_hi_last_2_min { get; set; }
		public int? wind_dir_at_hi_speed_last_2_min { get; set; }
		public double? wind_speed_avg_last_10_min { get; set; }
		public int wind_dir_scalar_avg_last_10_min { get; set; }
		public double? wind_speed_hi_last_10_min { get; set; }
		public int wind_dir_at_hi_speed_last_10_min { get; set; }
		public double? wind_run_day { get; set; }  // Type 23 only
		public int? rain_size { get; set; }
		public int? rain_rate_last_clicks { get; set; }
		public double rain_rate_last_in { get; set; }
		public double rain_rate_last_mm { get; set; }
		public int rain_rate_hi_clicks { get; set; }
		public double rain_rate_hi_in { get; set; }
		public double rain_rate_hi_mm { get; set; }
		public int rainfall_last_15_min_clicks { get; set; }
		public double rainfall_last_15_min_in { get; set; }
		public double rainfall_last_15_min_mm { get; set; }
		public int rain_rate_hi_last_15_min_clicks { get; set; }
		public double rain_rate_hi_last_15_min_in { get; set; }
		public double rain_rate_hi_last_15_min_mm { get; set; }
		public int rainfall_last_60_min_clicks { get; set; }
		public double rainfall_last_60_min_in { get; set; }
		public double rainfall_last_60_min_mm { get; set; }
		public double rainfall_last_24_hr_clicks { get; set; }
		public double rainfall_last_24_hr_in { get; set; }
		public double rainfall_last_24_hr_mm { get; set; }
		public double? rain_storm_clicks { get; set; }
		public double rain_storm_in { get; set; }
		public double rain_storm_mm { get; set; }
		public long? rain_storm_start_at { get; set; }
		public int? solar_rad { get; set; }
		public double? solar_energy_day { get; set; } // Type 23 only
		public double? et_day { get; set; } // Type 23 only
		public double? et_month { get; set; } // Type 23 only
		public double? et_year { get; set; } // Type 23 only
		public double? uv_index { get; set; }
		public double? uv_dose_day { get; set; } // Type 23 only
		public double? hdd_day { get; set; } // Type 23 only
		public double? cdd_day { get; set; } // Type 23 only
		public int? reception_day { get; set; } // Type 23 only
		public int? rssi_last { get; set; } // Type 23 only
		public int? crc_errors_day { get; set; } // Type 23 only
		public int? resyncs_day { get; set; } // Type 23 only
		public int? packets_received_day { get; set; } // Type 23 only
		public int? packets_received_streak { get; set; } // Type 23 only
		public int? packets_missed_day { get; set; } // Type 23 only
		public int? packets_missed_streak { get; set; } // Type 23 only
		public int? packets_received_streak_hi_day { get; set; } // Type 23 only
		public int? packets_missed_streak_hi_day { get; set; } // Type 23 only
		public int rx_state { get; set; }
		public int? freq_error_current { get; set; } // Type 23 only
		public int? freq_error_total { get; set; } // Type 23 only
		public int? freq_index { get; set; } // Type 23 only
		public long? last_packet_received_timestamp { get; set; } // Type 23 only
		public int? trans_battery_flag { get; set; }
		public double? trans_battery_volt { get; set; } // Type 23 only
		public double? solar_panel_volt { get; set; } // Type 23 only
		public double? supercap_volt { get; set; } // Type 23 only
		public double? spars_volt { get; set; } // Type 23 only
		public double? spars_rpm { get; set; } // Type 23 only
		public int rainfall_daily_clicks { get; set; }
		public double rainfall_daily_in { get; set; }
		public double rainfall_daily_mm { get; set; }
		public int rainfall_monthly_clicks { get; set; }
		public double rainfall_monthly_in { get; set; }
		public double rainfall_monthly_mm { get; set; }
		public int? rainfall_year_clicks { get; set; }
		public double rainfall_year_in { get; set; }
		public double rainfall_year_mm { get; set; }
		public int rain_storm_last_clicks { get; set; }
		public double rain_storm_last_in { get; set; }
		public double rain_storm_last_mm { get; set; }
		public int rain_storm_last_start_at { get; set; }
		public int rain_storm_last_end_at { get; set; }
		public long ts { get; set; }
	}

	// Data Structure 12 = WeatherLink Live Leaf/Soil current record
	// Data Structure 25 = WeatherLink console Leaf/Soil current record
	public class WLCurrentSensorDataType12_25
	{
		public double? temp_1 { get; set; }
		public double? temp_2 { get; set; }
		public double? temp_3 { get; set; }
		public double? temp_4 { get; set; }
		public double? moist_soil_1 { get; set; }
		public double? moist_soil_2 { get; set; }
		public double? moist_soil_3 { get; set; }
		public double? moist_soil_4 { get; set; }
		public double? wet_leaf_1 { get; set; }
		public double? wet_leaf_2 { get; set; }
		public int? reception_day { get; set; } // Type 25 only
		public int? rssi_last { get; set; } // Type 25 only
		public int? crc_errors_day { get; set; } // Type 25 only
		public int? resyncs_day { get; set; } // Type 25 only
		public int? packets_received_day { get; set; } // Type 25 only
		public int? packets_received_streak { get; set; } // Type 25 only
		public int? packets_missed_day { get; set; } // Type 25 only
		public int? packets_missed_streak { get; set; } // Type 25 only
		public int? packets_received_streak_hi_day { get; set; } // Type 25 only
		public int? packets_missed_streak_hi_day { get; set; } // Type 25 only
		public int? rx_state { get; set; } // Type 25 only
		public int? freq_error_current { get; set; } // Type 25 only
		public int? freq_error_total { get; set; } // Type 25 only
		public int? freq_index { get; set; } // Type 25 only
		public long? last_packet_received_timestamp { get; set; } // Type 25 only
		public int? trans_battery_flag { get; set; } // Type 25 only
		public long ts { get; set; }

		public object this[string name]
		{
			get
			{
				Type myType = typeof(WLCurrentSensorDataType12_25);
				PropertyInfo myPropInfo = myType.GetProperty(name);
				return myPropInfo.GetValue(this, null);
			}
		}
	}

	// Data structure 12 for sensor Type 242 = WeatherLink Live Baro current record
	// Data structure 19 for sensor Type 242 = WeatherLink Console Baro current record
	public class WlCurrentSensorDataType12_19Baro
	{
		public double? bar_sea_level { get; set; }
		public double? bar_trend { get; set; }
		public double? bar_absolute { get; set; }
		public double? bar_offset { get; set; }
		public long ts { get; set; }
	}

	// Data structure 12 for sensor Type 243 = WeatherLink Live Inside Temp current record
	// Data Structure 21 for sensor type 365 = WeatherLink Console Inside Temp current record
	public class WlCurrentSensorDataType12_21Temp
	{
		public double? temp_in { get; set; }
		public double? hum_in { get; set; }
		public double? dew_point_in { get; set; }
		public double? heat_index_in { get; set; }
		public double? wet_bulb_in { get; set; } // Type 21 only
		public double? wbgt_in { get; set; } // Type 21 only
		public long ts { get; set; }
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

	public class WlSensor(int sensorType, int lsid, int parentId, string name, string parentName)
	{
		public int SensorType { get; set; } = sensorType;
		public int LSID { get; set; } = lsid;
		public int ParentID { get; set; } = parentId;
		public string Name { get; set; } = name;
		public string ParentName { get; set; } = parentName;
	}


	// WeatherLink.com status
	public class WlComSystemStatus
	{
		public WlComSystemStatusResult result { get; set; }

		public string ToString(bool PrintFullMessage)
		{
			if (!PrintFullMessage)
			{
				return $"Weatherlink.com overall System Status: '{result.status_overall.status}', Updated: {result.status_overall.updated}";
			}

			var msg = new StringBuilder();

			msg.AppendLine($"Weatherlink.com overall System Status: '{result.status_overall.status}', Updated: {result.status_overall.updated}");
			if (result.status_overall.status_code != 100)
			{
				// If we are not OK, then find what isn't working
				msg.AppendLine("Individual subsystems: ");
				foreach (var subSys in result.status)
				{
					msg.AppendLine($"   Subsystem: {subSys.name}, status: {subSys.status}, last updated: {subSys.updated}");
				}

				if (result.incidents.Length > 0)
				{
					msg.AppendLine($"\nCurrent incidents:");
					foreach (var incident in result.incidents)
					{
						msg.AppendLine($"  {incident.name}");
						foreach (var message in incident.messages)
						{
							msg.AppendLine($"    {message.datetime} - {message.details}");
						}

						if (incident.components_affected.Length > 0)
						{
							msg.AppendLine($"  Affected components:");
							foreach (var component in incident.components_affected)
							{
								msg.AppendLine($"    {component.name}");
							}
						}
					}
				}
			}
			return msg.ToString();
		}
	}

	public class WlComSystemStatusResult
	{
		public WlComStatusOverall status_overall { get; set; }
		public WlComStatus[] status { get; set; }
		public WlComIncidents[] incidents { get; set; }
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

	public class WlComIncidents
	{
		public string name { get; set; }
		public DateTime datetime_open { get; set; }
		public WlComIncidentMessages[] messages { get; set; }
		public WlComAffected[] containers_affected { get; set; }
		public WlComAffected[] components_affected { get; set; }
	}

	public class WlComIncidentMessages
	{
		public string details { get; set; }
		public DateTime datetime { get; set; }
	}

	public class WlComAffected
	{
		public string name { get; set; }
	}
}
