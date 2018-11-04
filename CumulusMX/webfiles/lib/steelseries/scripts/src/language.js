/*
 * A language file for the starter SteelSeries gauges page for Cumulus
 *
 * Created by Mark Crossley, July 2011
 *
 * File encoding = UTF-8
*/
/*! Version: 2.5.0 - Updated: 16 December 2014 (missing Swedish entries from 2.3.1 added, Cloudbase entries added, added Czech) */

/*global gauges */
/*jshint jquery:true,unused:false*/

var LANG = LANG || {};

//======================================================================================================================
// English
//======================================================================================================================
LANG.EN = {
    canvasnosupport : "No HTML5 Canvas support in your browser... Sorry...<br>" +
                      "Try upgrading your browser to a more recent version - nearly all browsers support Canvas now, even IE9!<br><br>" +
                      "Redirecting you to an 'old' gauges page...",
    //
    led_title : "Remote sensor status unkonwn",
    led_title_ok : "Remote sensor OK",
    led_title_lost : "Remote sensor contact lost!",
    led_title_unknown : "Remote sensor status unknown!",
    led_title_offline: "The weather station is currently offline.",
    //
    weather   : "weather",
    latitude  : "Latitude",
    longitude : "Longitude",
    elevation : "Elevation",
    //
    statusStr : "Loading...",
    StatusMsg : "Downloading...",
    StatusHttp : "HTTP Request Failed",
    StatusRetry : "Retrying...",
    StatusRetryIn : "Retry in...",
    StatusTimeout : "Timed out",
    StatusPageLimit : "Page auto-update limit reached, click the status LED to continue",
    //
    StatusLastUpdate : "Last update",
    StatusMinsAgo : "minutes ago",
    StatusHoursAgo : "hours ago",
    StatusDaysAgo : "days ago",
    //
    realtimeCorrupt : "Text file download corrupted! Retrying...",
    //
    timer : "seconds",
    at : "at",
    //
    // MAXIMUM number of characters that can be used for the 'title' variables (such as 'LANG_EN.temp_title_out')
    //        11 characters  ===="12345678901"====  11 characters
    //
    temp_title_out : "Temperature",
    temp_title_in : "Inside Temp",
    temp_out_info : "Outside Temperature",
    temp_out_web : "Outside",
    temp_in_info : "Inside Temperature",
    temp_in_web : "Inside",
    temp_trend_info : "Temperature Trend",
    //
    dew_title : "Dew Point",
    dew_info : "Dew Point",
    dew_web : "Dew Point",
    apptemp_title : "Apparent",
    apptemp_info : "Apparent (Feels-Like) Temperature",
    apptemp_web : "Apparent",
    chill_title : "Wind Chill",
    chill_info : "Wind Chill",
    chill_web : "Wind Chill",
    heat_title : "Heat Index",
    heat_info : "Heat Index",
    heat_web : "Heat Index",
    humdx_title : "Humidex",
    humdx_info : "Humidex",
    humdx_web : "Humidex",
    //
    rain_title : "Rainfall",
    rrate_title : "Rain Rate",
    rrate_info : "Rain Rate",
    LastRain_info : "Last Rain",
    LastRainedT_info : "Today at",
    LastRainedY_info : "Yesterday at",
    //
    hum_title_out : "Humidity",
    hum_title_in : "Inside Hum",
    hum_out_info : "Outside Humidity",
    hum_in_info : "Inside Humidity",
    hum_out_web : "Outside",
    hum_in_web : "Inside",
    //
    baro_title : "Pressure",
    baro_info : "Barometric Pressure",
    baro_trend_info : "Pressure Trend",
    //
    wind_title : "Wind Speed",
    tenminavg_title : "Average Wind Speed",
    tenminavgwind_info : "Average wind speed (10 min)",
    maxavgwind_info : "Maximum average wind speed",
    tenmingust_info : "Gust (10 min)",
    maxgust_info : "Maximum gust",
    latest_title : "Latest Wind",
    latestwind_info : "Latest Wind Speed",
    bearing_info : "Bearing",
    latest_web : "Latest",
    tenminavg_web : "Average",
    dominant_bearing : "Dominant wind today",
    calm: "calm",
    windrose: "Wind Rose",
    windruntoday: "Wind run today",
    //
    uv_title : "UV Index",
    uv_levels : ["None",
                 "No danger",
                 "Moderate risk",
                 "High risk",
                 "Very high risk",
                 "Extreme risk"],
    uv_headlines : ["No measurable UV Index",
                    "No danger to the average person",
                    "Moderate risk of harm from unprotected sun exposure",
                    "High risk of harm from unprotected sun exposure",
                    "Very high risk of harm from unprotected sun exposure",
                    "Extreme risk of harm from unprotected sun exposure"],
    uv_details : ["It is still night time or it is a very cloudy day.",

                 "Wear sunglasses on bright days; use sunscreen if there is snow on the ground,<br>" +
                 "which reflects UV radiation, or if you have particularly fair skin.",

                 "Wear sunglasses and use SPF 30+ sunscreen, cover the body with clothing and<br>" +
                 "a hat, and seek shade around midday when the sun is most intense.",

                 "Wear sunglasses and use SPF 30+ sunscreen, cover the body with sun protective<br>" +
                 "clothing and a wide-brim hat, and reduce time in the sun from two hours before<br>" +
                 "to three hours after solar noon (roughly 11:00 AM to 4:00 PM during summer in<br>" +
                 "zones that observe daylight saving time).",

                 "Wear SPF 30+ sunscreen, a shirt, sunglasses, and a hat.<br>" +
                 "Do not stay out in the sun for too long.",

                 "Take all precautions, including: wear sunglasses and use SPF 30+ sunscreen,<br>" +
                 "cover the body with a long-sleeve shirt and trousers, wear a very broad hat, and<br>" +
                 "avoid the sun from two hours before to three hours after solar noon (roughly 11:00 AM<br>" +
                 "to 4:00 PM during summer in zones that observe daylight saving time)."],
    //
    solar_title : "Solar Radiation",
    solar_currentMax : "Current theoretical maximum reading",
    solar_ofMax : "of maximum",
    solar_maxToday : "Today's maximum reading",
    //
    cloudbase_title : "Cloud Base",
    cloudbase_popup_title : "Theoretical cloud base",
    cloudbase_popup_text : "The calculation is a simple one; 1000 feet for every 4.4 degrees Fahrenheit<br>" +
                           "difference between the temperature and the dew point. Note that this simply<br>" +
                           "gives the theoretical height at which Cumulus clouds would begin to form, the<br>" +
                           "air being saturated",
    feet: "feet",
    metres: "metres",
    //
    lowest_info : "Lowest",
    highest_info : "Highest",
    lowestF_info : "Lowest",     // for proper translation of feminine words
    highestF_info : "Highest",    // for proper translation of feminine words
    //
    RisingVeryRapidly : "Rising very rapidly",
    RisingQuickly : "Rising quickly",
    Rising : "Rising",
    RisingSlowly : "Rising slowly",
    Steady : "Steady",
    FallingSlowly : "Falling slowly",
    Falling : "Falling",
    FallingQuickly : "Falling quickly",
    FallingVeryRapidly : "Falling very rapidly",
    //
    maximum_info : "Maximum",
    max_hour_info : "Max per hour",
    minimum_info : "Minimum",
    //
    coords : ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"],
    compass : ["N", "NE", "E", "SE", "S", "SW", "W", "NW"],
    months : ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"]
};

//======================================================================================================================
// French by Ray of Tzouhalem-Maple Bay Weather, and Jacques of Weather by You!
//======================================================================================================================
LANG.FR = {
    canvasnosupport : "Votre navigateur ne supporte pas la fonction Canvas de HTML5 ...Désolé...<br>" +
                      "Une mise à jour de votre navigateur à une version plus récente est requise - presque tous les navigateurs supportent HTML5 maintenant, incluant IE9!<br><br>" +
                      "Je vous redirige vers le site contenant les anciennes jauges...",
    //
    led_title : "Statut du capteur - inconnu",
    led_title_ok : "Statut du capteur - OK",
    led_title_lost : "Contact avec le capteur - interrompu!",
    led_title_unknown : "Contact avec le capteur - inconnu!",
    led_title_offline : "La station météo est actuellement hors-ligne.",
    //
    weather   : "Météo",
    latitude  : "Latitude",
    longitude : "Longitude",
    elevation : "Élévation",
    //
    statusStr : "Chargement...",
    StatusMsg : "Téléchargement...",
    StatusHttp : "La requête HTTP a échoué",
    StatusRetry : "Réessai...",
    StatusRetryIn : "Tentative dans...",
    StatusTimeout : "Dépassement de temps",
    StatusPageLimit : "Délais de rafraîchissement automatique atteint, cliquez la DEL de status pour continuer",
    //
    StatusLastUpdate : "Dernière mise à jour - il y a",
    StatusMinsAgo : "minutes",
    StatusHoursAgo : "heures",
    StatusDaysAgo : "jours",
    //
    realtimeCorrupt : "Le téléchargement du fichier texte est corrompu! Réessai...",
    //
    timer : "secondes",
    at : "à",
    //
    temp_title_out : "Température",
    temp_title_in : "Intérieure",
    temp_out_info : "Température à l'extérieure",
    temp_out_web : "À l'extérieur",
    temp_in_info : "Température à l'intérieure",
    temp_in_web : "À l'intérieur",
    temp_trend_info : "Tendances de la température",
    //
    dew_title : "Point de rosée",
    dew_info : "Point de rosée",
    dew_web : "Point de rosée",
    //
    apptemp_title : "Sensation",
    apptemp_info : "Sensation - température ressentie",
    apptemp_web : "Sensation",
    //
    chill_title : "Refroidissement",
    chill_info : "Refroidissement éolien",
    chill_web : "Refroidissement",
    //
    heat_title : "Indice chaleur",
    heat_info : "Indice de chaleur",
    heat_web : "Indice chaleur",
    //
    humdx_title : "Humidex",
    humdx_info : "Humidex",
    humdx_web : "Humidex",
    //
    rain_title : "Précipitations",
    rrate_title : "Débit de pluie",
    rrate_info : "Débit de pluie",
    LastRain_info : "Dernière pluie",
    LastRainedT_info : "aujourd'hui à",
    LastRainedY_info : "hier à",
    //
    hum_title_out : "Humidité",
    hum_title_in : "Intérieure",
    hum_out_info : "Humidité à l'extérieur",
    hum_in_info : "Humidité à l'intérieur",
    hum_out_web : "À l'extérieur",
    hum_in_web : "À l'intérieur",
    //
    baro_title : "Pression",
    baro_info : "Pression barométrique",
    baro_trend_info : "tendance barométrique",
    //
    wind_title : "Vitesse du vent",
    tenminavg_title : "Vitesse moyenne",
    tenminavgwind_info : "Vitesse moyenne du vent (10 min)",
    maxavgwind_info : "vitesse moyenne maximale",
    tenmingust_info : "Rafale (10 min)",
    maxgust_info : "Rafale maximale",
    latest_title : "Vent récent",
    latestwind_info : "Vitesse du vent (récente)",
    bearing_info : "direction",
    latest_web : "Récente",
    tenminavg_web : "Moyenne",
    dominant_bearing : "Vent dominant aujourd'hui",
    calm: "calme",
    windrose: "Rose des Vents",
    windruntoday: "Parcours du Vent auj.",
    //
    uv_title : "l’indice UV",
    uv_levels : ["Nul",
                 "Bas",
                 "Modéré",
                 "Élevé",
                 "Très élevé",
                 "Extrême"],
    uv_headlines : ["Aucun indice UV quantifiable",
                    "Sans danger pour l'individu moyen",
                    "Peu de risques de brûlures causées par l'exposition au soleil sans protection",
                    "Haut risque de brûlures si exposition au soleil sans protection",
                    "Risque très élevé de brûlure si exposition au soleil sans protection",
                    "Très haut risque de brûlure si exposition au soleil sans protection"],
    uv_details : ["Il fait encore nuit ou c'est une journée très nuageuse.",

                  "Protection solaire minime requise pour les activités normales.<br>" +
                  "Portez des lunettes de soleil lors de journées ensoleillées. Si vous restez à l’extérieur pendant plus d’une heure, couvrez-vous et utilisez un écran solaire.<br>" +
                  "La réflexion par la neige peut presque doubler l’intensité des rayons UV. Portez des lunettes de soleil et appliquez un écran solaire sur votre visage.",

                  "Prenez des précautions : couvrez-vous, portez un chapeau et des lunettes de soleil, et appliquez un écran solaire, surtout si vous êtes à l’extérieur pendant 30 minutes ou plus.<br>" +
                  "Cherchez l’ombre à mi-journée, le soleil y est à son plus fort.",

                  "Protection nécessaire – les rayons UV endommagent la peau et peuvent causer des coups de soleil.<br>" +
                  "Évitez le soleil entre 11 h et 16 h, prenez toutes les précautions : cherchez l’ombre, couvrez-vous, portez un chapeau, des lunettes de soleil et appliquez un écran solaire.",

                  "Précautions supplémentaires nécessaires : la peau non protégée sera endommagée et peut brûler rapidement.<br>" +
                  "Évitez le soleil entre 11 h et 16 h. Cherchez l’ombre, couvrez-vous, portez un chapeau et des lunettes de soleil et appliquez un écran solaire.",

                  "Les valeurs de 11 ou plus sont très rares. Cependant, l’indice UV peut atteindre 14 ou plus dans les tropiques ou le sud des États-Unis.<br>" +
                  "Prenez toutes les précautions. La peau non protégée sera endommagée et peut brûler en quelques minutes. Évitez le soleil entre 11 h et 16 h, couvrez-vous, portez un chapeau et des lunettes de soleil et appliquez un écran solaire.<br>" +
                  "N’oubliez pas que le sable blanc et les autres surfaces brillantes réfléchissent les rayons UV et augmentent l’exposition à ces rayons."],
    //
    solar_title : "Rayonnement solaire",
    solar_currentMax : "Lecture maximale théorique courante",
    solar_ofMax : "du maximum",
    solar_maxToday : "Lecture maximale de la journée",
    //
    cloudbase_title : "Plafond nuageux",
    cloudbase_popup_title : "Plafond nuageux théorique",
    cloudbase_popup_text : "Le calcul est simple; 1000 pieds pour chaque différence de 4.4 degrés Fahrenheit<br>" +
                           "entre la température et le point de rosée. A noter que cela donne simplement la<br>" +
                           "hauteur théorique à laquelle des cumulus commenceront à se former, l'air étant saturé.",

    feet: "feet",
    metres: "mètres",
    //
    lowest_info : "le plus bas",
    highest_info : "le plus élevé",
    lowestF_info : "la plus basse",  // for proper translation of feminine words
    highestF_info : "la plus élevée", // for proper translation of feminine words
    //
    RisingVeryRapidly : "Augmentation très rapide",
    RisingQuickly : "Augmentation rapide",
    Rising : "Augmentation",
    RisingSlowly : "Augmentation lente",
    Steady : "Constante",
    FallingSlowly : "Baisse lentement",
    Falling : "Baisse",
    FallingQuickly : "Baisse rapidement",
    FallingVeryRapidly : "Baisse très rapidement",
    //
    maximum_info : "maximum",
    max_hour_info : "maximum par heure",
    minimum_info : "minimum",
    //
    coords : ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSO", "SO", "OSO", "O", "ONO", "NO", "NNO"],
    compass : ["N", "NE", "E", "SE", "S", "SO", "O", "NO"],
    months : ["Jan", "Fév", "Mar", "Avr", "Mai", "Jui", "Jul", "Aoû", "Sep", "Oct", "Nov", "Déc"]
};

//======================================================================================================================
// Deutsch by RASter
//======================================================================================================================
LANG.DE = {
    canvasnosupport : "Ihr Browser hat keine HTML5 Canvas Unterstützung.<br>" +
                      "Aktualisieren Sie Ihren Browser auf eine neuere Version, fast alle Browser Unterstützen Canvas Heute, sogar der IE9!<br><br>" +
                      "Sie werden auf eine 'Alte' Gauge Seite umgeleitet...",
    //
    led_title : "Fernsensor: Status unbekannt",
    led_title_ok : "Fernsensor: OK",
    led_title_lost : "Fernsensor: Kontakt verloren!",
    led_title_unknown : "Fernsensor: Status unbekannt!",
    led_title_offline : "Die Wetterstation ist derzeit offline.",
    //
    weather   : "Wetter",
    latitude  : "Breitengrad",
    longitude : "Längengrad",
    elevation : "Höhe",
    //
    statusStr : "Lade...",
    StatusMsg : "Lade Daten...",
    StatusHttp : "HTTP Anfrage fehlgeschlagen",
    StatusRetry : "Wiederhole...",
    StatusRetryIn : "Wiederhole in...",
    StatusTimeout : "Zeitüberschreitung",
    StatusPageLimit : "Seiten 'auto-update'-Zeitlimit erreicht, um fortzufahren aktuelle Seite neu laden",
    //
    StatusLastUpdate : "Letzte Aktualisierung vor",
    StatusMinsAgo : "Minuten",
    StatusHoursAgo : "Stunden",
    StatusDaysAgo : "Tagen",
    //
    realtimeCorrupt : "Geladene Textdatei ist beschädigt! Wiederhole...",
    //
    timer : "Sekunden",
    at : "um",
    //
    temp_title_out : "Temperatur",
    temp_title_in : "Temp. Innen",
    temp_out_info : "Außentemperatur",
    temp_out_web : "Außen",
    temp_in_info : "Innentemperatur",
    temp_in_web : "Innen",
    temp_trend_info : "Temperatur Trend",
    //
    dew_title : "Taupunkt",
    dew_info : "Taupunkt",
    dew_web : "Taupunkt",
    apptemp_title : "Gefühlt",
    apptemp_info : "Gefühlte Temperatur",
    apptemp_web : "Gefühlt",
    chill_title : "Windkälte",
    chill_info : "Windkälte",
    chill_web : "Windkälte",
    heat_title : "Hitzeindex",
    heat_info : "Hitzeindex",
    heat_web : "Hitzeindex",
    humdx_title : "Humidex",
    humdx_info : "Humidex",
    humdx_web : "Humidex",
    //
    rain_title : "Regen",
    rrate_title : "Regenmenge",
    rrate_info : "Regenmenge",
    LastRain_info : "Letzter Regen",
    LastRainedT_info : "Heute um",
    LastRainedY_info : "Gestern um",
    //
    hum_title_out : "Luftfeuchte",
    hum_title_in : "Luftfeuchte In",
    hum_out_info : "Luftfeuchte Außen",
    hum_in_info : "Luftfeuchte Innen",
    hum_out_web : "Außen",
    hum_in_web : "Innen",
    //
    baro_title : "Luftdruck",
    baro_info : "Barometrischer Luftdruck",
    baro_trend_info : "Luftdruck Trend",
    //
    wind_title : "Windstärke ",
    tenminavg_title : "Mittelwert Windstärke",
    tenminavgwind_info : "Mittelwert Windstärke (10 min)",
    maxavgwind_info : "Höchster Mittelwert Windstärke",
    tenmingust_info : "Windböe (10 min)",
    maxgust_info : "Höchste Windböe",
    latest_title : "Aktueller Wind",
    latestwind_info : "Aktuelle Windstärke",
    bearing_info : "Windrichtung",
    latest_web : "Aktuell",
    tenminavg_web : "Mittelwert",
    dominant_bearing : "vorherrschende Windrichtung Heute",
    calm: "calm",
    windrose: "Windrose",
    windruntoday: "Wind run today",
    //
    uv_title : "UV Index",
    uv_levels : ["keiner",
                 "keine Gefahr",
                 "kleines Risiko",
                 "hohes Risiko",
                 "sehr hohes Risiko",
                 "extremes Risiko"],
    uv_headlines : ["Kein messbarer UV Index",
                    "Keine Gefahr für die durchschnittliche Person",
                    "Kleines Risiko einer Schädigung durch ungeschützten Aufenthalt in der Sonne",
                    "Grosses Risiko einer Schädigung durch ungeschützten Aufenthalt in der Sonne",
                    "Sehr hohes Risiko einer Schädigung durch ungeschützten Aufenthalt in der Sonne",
                    "Extremes Risiko einer Schädigung durch ungeschützten Aufenthalt in der Sonne"],
    uv_details : ["Es ist entweder Nacht oder ein sehr bewölkter Tag.",

                  "Tragen Sie an hellen Tagen eine Sonnenbrille; verwenden Sie Sonnenschutzmittel<br>" +
                  "wenn Sie besonders helle Haut haben oder wenn Schnee liegt welcher UV_Strahlung reflektiert.",

                  "Tragen Sie eine Sonnenbrille und verwenden Sie Sonnencreme<br>" +
                  "mit Lichtschutzfaktor 30 oder höher. Bedecken Sie den Körper mit Kleidung und tragen Sie einen Hut.<br>" +
                  "Suchen Sie um die Mittagszeit, wenn die Sonne am stärksten ist, Schatten auf.",

                  "Tragen Sie eine Sonnenbrille und verwenden Sie Sonnencreme<br>" +
                  "mit Lichtschutzfaktor 30 und höher. Schützen Sie den Körper durch entsprechende Kleidung<br>" +
                  "und einen breitkrempigen Hut. Begrenzen Sie den Aufenthalt in der Sonne zwei Stunden vor bis drei Stunden nach<br>" +
                  "dem solaren Mittag (ca. 11:00 - 16:00 Uhr in Ländern mit Sommerzeit) auf ein Minimum",

                  "Benutzen Sie Sonnencreme, Lichtschutzfaktor 30 und höher.<br>" +
                  "Tragen Sie eine Sonnenbrille, T-Shirt und einen Hut.<br>" +
                  "Bleiben Sie nicht zu lange in der Sonne.",

                  "Treffen Sie alle Vorsichtsmaßnahmen: tragen Sie eine Sonnenbrille und benutzen Sie Sonnencreme,<br>" +
                  "Lichtschutzfaktor 30 oder höher, bedecken Sie den Körper mit einem langärmeligen T-Shirt, tragen Sie lange Hosen und einen breiten Hut, <br>" +
                  "meiden Sie die Sonne zwei Stunden vor bis drei Stunden nach dem solaren Mittag (ca. 11:00 - <br>" +
                  "16:00 Uhr in Ländern mit Sommerzeit)."],
    //
    solar_title : "Sonnenstrahlung",
    solar_currentMax : "Aktueller theoretischer maximaler Messwert",
    solar_ofMax : "vom Maximum",
    solar_maxToday : "Heutiger maximaler Messwert",
    //
    cloudbase_title : "Wolkenuntergrenze",
    cloudbase_popup_title : "Theoretische Wolkenuntergrenze",
    cloudbase_popup_text : "Die Berechnung ist einfach; 1000 Fuß für jede 4,4 Grad Fahrenheit<br>" +
                           "Unterschied zwischen der Temperatur und dem Taupunkt. Beachte das<br>" +
                           "dies nur die theoretische Höhe angibt in der sich Cumulus Wolken formen<br>" +
                           "wenn die Luft gesättigt ist",
    feet: "Fuß",
    metres: "Meter",
    //
    lowest_info : "Niedrigster",
    highest_info : "Höchster",
    lowestF_info : "Niedrigste",     // for proper translation of feminine words
    highestF_info : "Höchste",    // for proper translation of feminine words
    //
    RisingVeryRapidly : "sehr schnell steigend",
    RisingQuickly : "schnell steigend",
    Rising : "steigend",
    RisingSlowly : "langsam steigend ",
    Steady : "konstant",
    FallingSlowly : "langsam fallend",
    Falling : "fallend",
    FallingQuickly : "schnell fallend",
    FallingVeryRapidly : "sehr schnell fallend",
    //
    maximum_info : "Maximum",
    max_hour_info : "Max pro Stunde",
    minimum_info : "Minimum",
    //
    coords : ["N", "NNO", "NO", "ONO", "O", "OSO", "SO", "SSO", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"],
    compass : ["N", "NO", "O", "SO", "S", "SW", "W", "NW"],
    months : ["Jan", "Feb", "Mar", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez"]
};

//======================================================================================================================
// Nederlands (Dutch)
//======================================================================================================================
LANG.NL = {
    canvasnosupport : "Uw browser ondersteunt geen HTML5 Canvas .<br>" +
                      "Gebruik een modernere browser of de laatste versie, bijna alle browsers ondersteunen Canvas nu, zelfs IE9!<br><br>" +
                      "Je wordt naar de 'oude' meters omgeleid ...",
    //
    led_title : "Buitensensor status onbekend",
    led_title_ok : "Buitensensor OK",
    led_title_lost : "Buitensensor kontakt verloren!",
    led_title_unknown : "Buitensensor status onbekend!",
    led_title_offline : "Het weerstation is momenteel offline.",
    //
    weather   : "Weer",
    latitude  : "Breedtegraad",
    longitude : "Lengtegraad",
    elevation : "Hoogte",
    //
    statusStr : "Laden..",
    StatusMsg : "Data laden...",
    StatusHttp : "HTTP Aanvraag afgebroken",
    StatusRetry : "Opnieuw...",
    StatusRetryIn : "Opnieuw over..",
    StatusTimeout : "Tijd voorbij",
    StatusPageLimit : "Pagina auto-update limiet bereikt, pagina herladen om door te gaan",
    //
    StatusLastUpdate : "Laatste update",
    StatusMinsAgo : "minuten geleden",
    StatusHoursAgo : "uren geleden",
    StatusDaysAgo : "dagen geleden",
    //
    realtimeCorrupt : "Data zijn fout! Opnieuw...",
    //
    timer : "Seconden",
    at : "om",
    //
    temp_title_out : "Temperatuur",
    temp_title_in : "Binnentemperatuur",
    temp_out_info : "Buitentemperatuur",
    temp_out_web : "Buiten",
    temp_in_info : "Binnentemperatuur",
    temp_in_web : "Binnen",
    temp_trend_info : "Temperatuurtrend",
    //
    dew_title : "Dauwpunt",
    dew_info : "Dauwpunt",
    dew_web : "Dauwpunt",
    apptemp_title : "Gevoel",
    apptemp_info : "Gevoelstemperatuur",
    apptemp_web : "Gevoel",
    chill_title : "Windchill",
    chill_info : "Windchill",
    chill_web : "Windchill",
    heat_title : "Hitteindex",
    heat_info : "Hitteindex",
    heat_web : "Hitteindex",
    humdx_title : "Humidex",
    humdx_info : "Humidex",
    humdx_web : "Humidex",
    //
    rain_title : "Regen",
    rrate_title : "Regenintensiteit",
    rrate_info : "Regenintensiteit",
    LastRain_info : "Laatste regen",
    LastRainedT_info : "Vandaag om",
    LastRainedY_info : "Gisteren om",
    //
    hum_title_out : "Luchtvochtigheid",
    hum_title_in : "Luchtvochtigheid binnen",
    hum_out_info : "Luchtvochtigheid buiten",
    hum_in_info : "Luchtvochtigheid binnen",
    hum_out_web : "Buiten",
    hum_in_web : "Binnen",
    //
    baro_title : "Luchtdruk",
    baro_info : "Barometer luchtdruk",
    baro_trend_info : "Barometer trend",
    //
    wind_title : "Wind ",
    tenminavg_title : "Gemiddelde windsnelheid",
    tenminavgwind_info : "Gemiddelde windsnelheid (10 min)",
    maxavgwind_info : "Hoogste gemiddelde windsnelheid",
    tenmingust_info : "Wind (10 min)",
    maxgust_info : "Zwaarste windvlagen",
    latest_title : "Huidige",
    latestwind_info : "Huidige windsnelheid",
    bearing_info : "Windrichting",
    latest_web : "Momenteel",
    tenminavg_web : "Gemiddelde",
    dominant_bearing : "Overheersende Windrichting",
    calm: "kalm",
    windrose: "Windroos",
    windruntoday: "Wind run vandaag",
    //
    uv_title : "UV-Index",
    uv_levels : ["Geen",
                 "Geen gevaar",
                 "Weinig gevaar",
                 "Hoog risico",
                 "Zeer hoog risico",
                 "Extreem risico"],
    uv_headlines : ["Geen meetbare UV Index",
                    "Geen gevaar voor de gewone mens",
                    "Kleine kans op zonnebrand bij onbeschermde huid",
                    "Hoog risico op zonnebrand bij onbeschermde huid",
                    "Zeer hoog risico op zonnebrand bij onbeschermde huid",
                    "Extreem risico op zonnebrand bij onbeschermde huid"],
    uv_details : ["Het is nog steeds nacht of het is een zeer bewolkte dag.",

                  "Draag een zonnebril bij heldere dagen, gebruik zonnecreme bij een sneeuwlaag,<br>" +
                  "omdat die UV-straling terugkaatst, of bij zeer bleke huid.",

                  "Draag een zonnebril en gebruik creme met beschermingsfactor 30 of hoger, draag beschermende kledij en<br>" +
                  "een hoed en ga uit de zon tijdens de middaguren wanneer de zon op zijn hoogst staat.",

                  "Draag een zonnebril en gebruik creme met beschermingsfactor 30 of hoger, smeer u goed in<br>" +
                  "draag beschermende kledij met een breedgerande hoed, beperk uw tijd in de zon van twee uur voor<br>" +
                  "tot drie uur na de middagzon (ongeveer van 11:00 uur tot 16:00 uur tijdens de zomertijd.",

                  "Draag een zonnebril en gebruik creme met beschermingsfactor 30 of hoger, beschermende kledij en een hoed.<br>" +
                  "Blijf niet te lang in de zon.",

                  "Neem alle voorzorgen, waaronder: zonnebril, zonnecreme met beschermingsfactor 30 of meer,<br>" +
                  "een t-shirt met lange mouwen, een broek en een breedgerande hoed, en<br>" +
                  "vermijd de middagzon vanaf 11:00 uur tot 16:00 uur, tijdens de zomer."],
    //
    solar_title : "Zonnestraling",
    solar_currentMax : "Huidige theoretische maximale waarde",
    solar_ofMax : "van maximum",
    solar_maxToday : "Maximale  waarde vandaag",
    //
    cloudbase_title : "Wolkenbasis",
    cloudbase_popup_title : "Theoretische wolkenbasis",
    cloudbase_popup_text : "De berekening is ongecompliceerd; 1000 voet voor elke 4.4 graden Fahrenheit<br>" +
                           "verschil tussen de temperatuur en dauwpunt. Let op dat dit eenvoudig<br>" +
                           "de theoretische hoogte weergeeft wanneer Cumulus wolken zich zouden vormen, en de<br>" +
                           "lucht verzadigd wordt",
    feet: "feet",
    metres: "meter",
    //
    lowest_info : "Laagste",
    highest_info : "Hoogste",
    lowestF_info : "Laagste",     // for proper translation of feminine words
    highestF_info : "Hoogste",    // for proper translation of feminine words
    //
    RisingVeryRapidly : "Zeer snel stijgend",
    RisingQuickly : "Snel stijgend",
    Rising : "Stijgend",
    RisingSlowly : "Langzaam stijgend",
    Steady : "Constant",
    FallingSlowly : "Langzaam dalend",
    Falling : "Dalend",
    FallingQuickly : "Snel dalend",
    FallingVeryRapidly : "Zeer snel dalend",
    //
    maximum_info : "Maximum",
    max_hour_info : "Max per uur",
    minimum_info : "Minimum",
    //
    coords : ["N", "NNO", "NO", "ONO", "O", "OZO", "ZO", "ZZO", "Z", "ZZW", "ZW", "WZW", "W", "WNW", "NW", "NNW"],
    compass : ["N", "NO", "O", "ZO", "Z", "ZW", "W", "NW"],
    months : ["Jan", "Feb", "Mrt", "Apr", "Mei", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dec"]
};

//======================================================================================================================
// Swedish
//======================================================================================================================
LANG.SE = {
    canvasnosupport : "Tyvärr... Ingen HTML5 Canvas support i din webläsare...<br>" +
                      "Pröva att uppgradera din webläsare till en senare version!<br><br>" +
                      "Styr om till en äldre sida med mätare...",
    //
    led_title : "Väderstationens status är okänd",
    led_title_ok : "Väderstationen OK",
    led_title_lost : "Har tappat kontakten med väderstationen!",
    led_title_unknown : "Väderstationens status är okänd!",
    led_title_offline : "Väderstationen är för närvarande ej nåbar.",
    //
    weather   : "v&#228;der",
    latitude  : "Latitud",
    longitude : "Longitud",
    elevation : "Altitud",
    //
    statusStr : "Laddar...",
    StatusMsg : "Laddar ned...",
    StatusHttp : "HTTP-begäran misslyckades",
    StatusRetry : "Försöker på nytt...",
    StatusRetryIn : "Försök om...",
    StatusTimeout : "Timed out",
    StatusPageLimit : "Page auto-update limit reached, refresh your browser to continue",
    //
    StatusLastUpdate : "Senaste uppdatering för",
    StatusMinsAgo : "minuter sedan",
    StatusHoursAgo : "timmar sedan",
    StatusDaysAgo : "dagar sedan",
    //
    realtimeCorrupt : "Den nedladdade filen är felaktig! Försöker på nytt...",
    //
    timer : "sekunder",
    at : "vid",
    //
    temp_title_out : "Temperatur",
    temp_title_in : "Innetemp",
    temp_out_info : "Utetemp",
    temp_out_web : "Ute",
    temp_in_info : "Innetemp",
    temp_in_web : "Inne",
    temp_trend_info : "Trend - temp",
    //
    dew_title : "Daggpunkt",
    dew_info : "Daggpunkt",
    dew_web : "Daggpunkt",
    apptemp_title : "Upplevd",
    apptemp_info : "Upplevd (känns som) temperatur",
    apptemp_web : "Upplevd",
    chill_title : "Köldeffekt",
    chill_info : "Köldeffekt",
    chill_web : "Köldeffekt",
    heat_title : "Värmeindex",
    heat_info : "Värmeindex",
    heat_web : "Värmeindex",
    humdx_title : "Fuktindex",
    humdx_info : "Fuktindex",
    humdx_web : "Fuktindex",
    //
    rain_title : "Nederbörd",
    rrate_title : "Nederbörd",
    rrate_info : "Nederbördintensitet",
    LastRain_info : "Senaste nederbörd",
    LastRainedT_info : "Idag vid",
    LastRainedY_info : "Igår vid",
    //
    hum_title_out : "Fukt. ute",
    hum_title_in : "Fukt. inne",
    hum_out_info : "Fukt. ute",
    hum_in_info : "Fukt. inne",
    hum_out_web : "Fukt. ute",
    hum_in_web : "Fukt. inne",
    //
    baro_title : "Lufttryck",
    baro_info : "Lufttryck",
    baro_trend_info : "Trend - lufttryck",
    //
    wind_title : "Vind",
    tenminavg_title : "Medelvind",
    tenminavgwind_info : "Medelvind (10 min)",
    maxavgwind_info : "Max medelvind",
    tenmingust_info : "Byvind (10 min)",
    maxgust_info : "Max byvind",
    latest_title : "Senaste vind",
    latestwind_info : "Senaste vind",
    bearing_info : "Riktning",
    latest_web : "Senaste",
    tenminavg_web : "Medel",
    dominant_bearing : "Dominerande vindriktning",
    calm: "lugn",
    windrose: "Vindros",
    windruntoday: "Vindsträcka",
    //
    uv_title : "UV Index",
    uv_levels : ["None",
                 "Ingen fara",
                 "Liten risk",
                 "Hög risk",
                 "Mycket hög risk",
                 "Extrem risk"],
    uv_headlines : ["No measurable UV Index",
                    "Ingen fara för normalindividen",
                    "Liten skaderisk vid oskyddad solexponering",
                    "Hög skaderisk vid oskyddad solexponering",
                    "Väldigt hög skaderisk vid oskyddad solexponering",
                    "Extrem skaderisk vid oskyddad solexponering"],
    uv_details : ["It is still night time or it is a very cloudy day.",

                  "Använd solglasögon molnfria dagar, använd solskydd om det finns snö på marken,<br>" +
                  "som reflekterar UV-strålningen, eller om du har omtålig hy.",

                  "Använd solglasögon och solskyddsfaktor 30+, täck hud med klädsel och<br>" +
                  "huvudbonad, sök skugga vid middagstid då solen är som intensivast.",

                  "Använd solglasögon och solskyddsfaktor 30+, täck hud med solskyddande klädsel och<br>" +
                  "bredbrättad huvudbonad, begränsa tiden i solen under två timmar före<br>" +
                  "och tre timmar efter då solen står som högst (~11:00 till 16:00 under sommarperioden i<br>" +
                  "områden med sommartid).",

                  "Använd solskyddsfaktor 30+, skjorta, solglasögon och huvudbonad.<br>" +
                  "Vistas inte utomhus onödigt mycket.",

                  "Vidtag alla försiktighetsåtgärder: använd solglasögon och solskyddsfaktor 30+,<br>" +
                  "täck kroppen med långärmad skjorta och långbyxor, använd bredbrättad huvudbonad,<br>" +
                  "undvik solen under två timmar före och tre timmar efter då solen står som högst (~11:00 till<br>" +
                  "16:00 under sommarperioden i områden med sommartid)."],
    //
    solar_title : "Solar Radiation",
    solar_currentMax : "Current theoretical maximum reading",
    solar_ofMax : "of maximum",
    solar_maxToday : "Today's maximum reading",
    //
    cloudbase_title : "Molnbas",
    cloudbase_popup_title : "Teoretisk molnbas",
    cloudbase_popup_text : "Beräkningen är enkel; 1000 fot för varje 4.4 grader Fahrenheit<br>" +
                           "skillnaden mellan temperaturen och daggpunkten. Observera att detta bara<br>" +
                           "ger den teoretiska höjden där Cumulusmoln skulle börja bildas, när<br>" +
                           "luften är mättad",
    feet: "feet",
    metres: "meter",
    //
    lowest_info : "Lägsta",
    highest_info : "Högsta",
    lowestF_info : "Lägsta",     // for proper translation of feminine words
    highestF_info : "Högsta",    // for proper translation of feminine words
    //
    RisingVeryRapidly : "Stiger mycket snabbt",
    RisingQuickly : "Stiger snabbt",
    Rising : "Stigande",
    RisingSlowly : "Stigande långsamt",
    Steady : "Konstant",
    FallingSlowly : "Faller långsamt",
    Falling : "Fallande",
    FallingQuickly : "Fallande snabbt",
    FallingVeryRapidly : "Fallande mycket snabbt",
    //
    maximum_info : "Max",
    max_hour_info : "Max per timma",
    minimum_info : "Min",
    //
    coords : ["N", "NNO", "NO", "ONO", "O", "OSO", "SO", "SSO", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"],
    compass : ["N", "NO", "O", "SO", "S", "SW", "W", "NW"],
    months : ["Jan", "Feb", "Mar", "Apr", "Maj", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dec"]
};

//======================================================================================================================
// Danish
// ***INCOMPLETE***
//======================================================================================================================
LANG.DK = {
    canvasnosupport : "Din browser understøtter ikke HTML5 lærred...<br>" +
                      "Opdatere din browser til en nyere version - næsten alle browsere understøter lærred nu - selv Internet Explorer 9!<br><br>" +
                      "Du omdirigeres til vores gamle forside...",
    //
    led_title : "Status ukendt",
    led_title_ok : "Status OK",
    led_title_lost : "Ingen kontakt til vejrstationen!",
    led_title_unknown : "Status ukendt!",
    led_title_offline: "Vejrstationen er pt. offline.",
    //
    weather   : "vejret",
    latitude  : "Breddegrad",
    longitude : "Længdegrad",
    elevation : "Højde over havet",
    //
    statusStr : "Henter...",
    StatusMsg : "Downloader...",
    StatusHttp : "HTTP forespørgsel fejlede",
    StatusRetry : "Prøver igen...",
    StatusRetryIn : "Prøver om...",
    StatusTimeout : "Timed out",
    StatusPageLimit : "Sidens grænse for opdateringer blev nået, opdater din browser for at fortsætte",
    //
    StatusLastUpdate : "Seneste opdatering",
    StatusMinsAgo : "minutter siden",
    StatusHoursAgo : "timer siden",
    StatusDaysAgo : "dage siden",
    //
    realtimeCorrupt : "Fejl på vejrdatafilen - prøver igen...",
    //
    timer : "sekunder",
    at : "kl.",
    //
    // MAXIMUM number of characters that can be used for the 'title' variables (such as 'LANG_EN.temp_title_out')
    //        11 characters  ===="12345678901"====  11 characters
    //
    temp_title_out : "Temperatur",
    temp_title_in : "Inde temp",
    temp_out_info : "Ude temperatur",
    temp_out_web : "Ude",
    temp_in_info : "Inde temperatur",
    temp_in_web : "Inde",
    temp_trend_info : "Temperatur tendens",
    //
    dew_title : "Dugpunkt",
    dew_info : "Dugpunkt",
    dew_web : "Dugpunkt",
    apptemp_title : "Komfort",
    apptemp_info : "Komfort (føles som) temperatur",
    apptemp_web : "Komfort",
    chill_title : "Vindchill",
    chill_info : "Vindchill",
    chill_web : "Vindchill",
    heat_title : "Hedeindex",
    heat_info : "Hedeindex",
    heat_web : "Hedeindex",
    humdx_title : "Humidex",
    humdx_info : "Humidex",
    humdx_web : "Humidex",
    //
    rain_title : "Regn",
    rrate_title : "Regnrate",
    rrate_info : "Regnrate",
    LastRain_info : "Seneste regn",
    LastRainedT_info : "I dag kl.",
    LastRainedY_info : "I går kl.",
    //
    hum_title_out : "Fugtighed",
    hum_title_in : "Inde fugt",
    hum_out_info : "Ude fugtighed",
    hum_in_info : "Inde fugtighed",
    hum_out_web : "Ude",
    hum_in_web : "Inde",
    //
    baro_title : "Barometer",
    baro_info : "Barometer",
    baro_trend_info : "Barometer tendens",
    //
    wind_title : "Vindhastighed",
    tenminavg_title : "Gennemsnitlig vindhastighed",
    tenminavgwind_info : "Gennemsnitlig vindhastighed (10 min)",
    maxavgwind_info : "Maximum gennemsnitlig vindhastighed",
    tenmingust_info : "Vindstød (10 min)",
    maxgust_info : "Maximum vindstød",
    latest_title : "Seneste vind",
    latestwind_info : "Seneste vindhastighed",
    bearing_info : "Retning",
    latest_web : "Seneste",
    tenminavg_web : "Gennemsnit",
    dominant_bearing : "Dominant wind today",
    calm: "calm",
    windrose: "Wind Rose",
    windruntoday: "Wind run today",
    //
    uv_title : "UV index",
    uv_levels : ["Ingen",
                 "Ingen fare",
                 "Lille risiko",
                 "Høj risiko",
                 "Meget høj risiko",
                 "Ekstrem risko"],
    uv_headlines : ["Ingen målbar UV index",
                    "Ingen fare for personskade",
                    "Lille risiko for skade hvis der ikke bruges solbeskyttelse",
                    "Stor risiko for skade, hvis der ikke bruges solbeskyttelse",
                    "Meget stor risiko for skade, hvis der ikke bruges solbeskyttelse",
                    "Ekstrem risiko for skade, hvis der ikke bruges solbeskyttelse"],
    uv_details : ["Det er stadig mørkt - eller en meget skyet dag.",

                 "Brug solbriller på lyse dage, brug solcreme hvis der er sne på jorden,<br/>" +
                 "som reflekterer UV stråling, eller hvis du har en særlig sart hud.",

                 "Brug solbriller og solcreme faktor 30+, tildæk kroppen med tøj og<br/>" +
                 "hat, og søg skygge mellem 12 og 14, hvor solen er mest intens.",

                 "Brug solbriller og solcreme faktor 30+, tildæk kroppen med solbeskyttende<br/>" +
                 "tøjh og brug en bredskygget hat. Søg skygge mellem 11 og 15<br/>",

                 "Brug solcreme faktor 30+, trøje, solbriller og en hat.<br/>" +
                 "Ophold dig ikke i solen i lang tid.",

                 "Tag alle forholdsregler: Brug solbriller og solcreme faktor 30+,<br/>" +
                 "tildæk korppen med langærmet trøje og bukser. Brug en meget stor hat og<br/>" +
                 "undgå solen mellem 11 og 15"],
    //
    solar_title : "Solstråling",
    solar_currentMax : "Max. muligt",
    solar_ofMax : "af maximum",
    solar_maxToday : "Maximum i dag",
    //
    cloudbase_title : "skyhøjde basen",
    cloudbase_popup_title : "Theoretical cloud base",
    cloudbase_popup_text : "The calculation is a simple one; 1000 feet for every 4.4 degrees Fahrenheit<br>" +
                           "difference between the temperature and the dew point. Note that this simply<br>" +
                           "gives the theoretical height at which Cumulus clouds would begin to form, the<br>" +
                           "air being saturated",
    feet: "feet",
    metres: "meters",
    //
    lowest_info : "Laveste",
    highest_info : "Højeste",
    lowestF_info : "Laveste",     // for proper translation of feminine words
    highestF_info : "Højeste",    // for proper translation of feminine words
    //
    RisingVeryRapidly : "Meget hurtigt stigende",
    RisingQuickly : "Hurtigt stigende",
    Rising : "Stigende",
    RisingSlowly : "Langsomt stigende",
    Steady : "Stabil",
    FallingSlowly : "Langsomt faldende",
    Falling : "Faldende",
    FallingQuickly : "Hurtigt faldende",
    FallingVeryRapidly : "Meget hurtigt faldende",
   //
    maximum_info : "Maximum",
    max_hour_info : "Max pr. time",
    minimum_info : "Minimum",
    //
    coords : ["N", "NNØ", "NØ", "ØNØ", "Ø", "ØSØ", "SØ", "SSØ", "S", "SSV", "SV", "VSV", "V", "VNV", "NV", "NNV"],
    compass : ["N", "NØ", "Ø", "SØ", "S", "SV", "V", "NV"],
    months : ["Jan", "Feb", "Mar", "Apr", "MaJ", "Jun", "Jul", "Aug", "Sep", "OKt", "Nov", "Dec"]
};

//======================================================================================================================
// Finnish
//======================================================================================================================
LANG.FI = {
    canvasnosupport : "Selaimesi ei tue HTML5 Canvas tekniikkaa... Sorry...<br>" +
                      "Päivitä selain uudempaan versioon - melkein kaikki tukevat Canvas tekniikkaa, myös IE9!<br><br>" +
                      "Siirrytään sivulle ilman HMTL5/Canvas mittareita...",
    //
    led_title : "Päivityksen tila tuntematon",
    led_title_ok : "Päivitys OK",
    led_title_lost : "Tietoliikenneyhteys poikki!",
    led_title_unknown : "Päivityksen tila tuntematon!",
    led_title_offline: "Sääasema on offline tilassa.",
    //
    weather   : "sää",
    latitude  : "Latitude",
    longitude : "Longitude",
    elevation : "Korkeus merenpinnasta",
    //
    statusStr : "Ladataan...",
    StatusMsg : "Ladataan...",
    StatusHttp : "HTTP pyyntö epäonnistui",
    StatusRetry : "Yritetään uudelleen...",
    StatusRetryIn : "Uusi yritys...",
    StatusTimeout : "Aikakatkaistiin",
    StatusPageLimit : "Sivun automaattisten latausten lukumäärä on saavutettu, päivitä selaimen näkymä jatkaaksesi!",
    //
    StatusLastUpdate : "Päivitetty",
    StatusMinsAgo : "minuuttia sitten",
    StatusHoursAgo : "tuntia sitten",
    StatusDaysAgo : "päivää sitten",
    //
    realtimeCorrupt : "Tekstitiedoston lataus epäonnistui! Yritetään uudelleen...",
    //
    timer : "sekuntia",
    at : "klo.",
    //
    // MAXIMUM number of characters that can be used for the 'title' variables (such as 'LANG_EN.temp_title_out')
    //        11 characters  ===="12345678901"====  11 characters
    //
    temp_title_out : "Lämpötila",
    temp_title_in : "Sisälämpötila",
    temp_out_info : "Ulkolämpötila",
    temp_out_web : "Ulko",
    temp_in_info : "Sisälämpötila",
    temp_in_web : "Sisä",
    temp_trend_info : "Lämpötilan muutos",
    //
    dew_title : "Kastepiste",
    dew_info : "Kastepiste",
    dew_web : "Kastepiste",
    apptemp_title : "Näennäinen",
    apptemp_info : "Näennäinen (tuntuu-kuin) lämpötila",
    apptemp_web : "Näennäinen",
    chill_title : "Hyytävyys",
    chill_info : "Hyytävyys",
    chill_web : "Hyytävyys",
    heat_title : "Tukaluus",
    heat_info : "Tukaluus",
    heat_web : "Tukaluus",
    humdx_title : "Kosteusindeksi",
    humdx_info : "Kosteusindeksi",
    humdx_web : "Kosteusindeksi",
    //
    rain_title : "Sademäärä",
    rrate_title : "Voimakkuus",
    rrate_info : "Sateen voimakkuus",
    LastRain_info : "Viimeksi satanut",
    LastRainedT_info : "Tänään",
    LastRainedY_info : "Eilen",
    //
    hum_title_out : "Ilmankosteus",
    hum_title_in : "Sisäilmankosteus",
    hum_out_info : "Ilmankosteus",
    hum_in_info : "Sisäilmankosteus",
    hum_out_web : "Ulko",
    hum_in_web : "Sisä",
    //
    baro_title : "Ilmanpaine",
    baro_info : "Ilmanpaine",
    baro_trend_info : "Ilmanpaineen muutos",
    //
    wind_title : "Tuulen nopeus",
    tenminavg_title : "Keskimääräinen tuulen nopeus",
    tenminavgwind_info : "Keskimääräinen tuulen nopeus (10 min)",
    maxavgwind_info : "Korkein keskituulen nopeus",
    tenmingust_info : "Puuskatuuli (10 min)",
    maxgust_info : "Kovin tuulen puuska",
    latest_title : "Viimeisin tuuli",
    latestwind_info : "Viimeisin tuulen nopeus",
    bearing_info : "Suunta",
    latest_web : "Viimeisin",
    tenminavg_web : "Keskimäärin",
    dominant_bearing : "Hallitseva tuulen suunta tänään",
    calm: "tyyntä",
    windrose: "Tuuliruusu",
    windruntoday: "Tuulen matka tänään",
    //
    uv_title : "UV Indeksi",
    uv_levels : ["n/a",
                 "Heikko",
                 "Kohtalainen",
                 "Voimakas",
                 "Hyvin voimakas",
                 "Äärimmäisen voimakas"],
    uv_headlines : ["Ei mitattavissa olevaa UV säteilyä",
                    "Ei vaaraa normaali henkilölle",
                    "Vähäinen vaara suojaamattomassa auringonpaisteessa",
                    "Vaara suojaamattomassa auringonpaisteessa",
                    "Suuri vaara suojaamattomassa auringonpaisteessa",
                    "Äärimmäisen suuri vaara suojaamattomassa auringonpaisteessa"],
    uv_details : ["On yöaika tai erittäin pilvistä.",

                 "Käytä aurinkolaseja. Käytä aurinkosuojavoidetta jos maassa on lunta tai jos ihosi on erityisen herkkä.",

                 "Käytä aurinkolaseja ja aurinkosuojavoidetta. Suojaa iho vaatteilla ja päähineellä <br>" +
                 "ja pysyttele varjoisassa ympäristössä keskipäivän tienoilla, kun auringon paiste on voimakkaimmillaan.",

                 "Käytä aurinkolaseja ja aurinkosuojavoidetta, jonka suojakerroin on vähintään 15. <br>" +
                 "Suojaa iho vaatteilla ja leveälierisellä päähineellä. Vähennä ulkonaoloa kello 11:n ja 16:n välillä.",

                 "Käytä aurinkolaseja ja aurinkosuojavoidetta, jonka suojakerroin on vähintään 15. <br>" +
                 "Suojaa iho vaatteilla ja leveälierisellä päähineellä. Vähennä ulkonaoloa kello 11:n ja 16:n välillä.",

                "Varaudu säteilyyn kaikin mahdollisin tavoin, käytä aurinkolaseja ja aurinkovoidetta, <br>" +
                "suojaa vartalo pitkähihaisella paidalla ja pitkillä housuilla, käytä leveälieristä päähinettä <br>" +
                "ja vältä aurinkoa kello 11:n ja 16:n välillä."],
    //
    solar_title : "Auringon säteilyteho",
    solar_currentMax : "Teorettinen maksimisäteily nyt",
    solar_ofMax : "maksimista",
    solar_maxToday : "Maksimisäteily tänään",
    //
    cloudbase_title : "Pilvien korkeus",
    cloudbase_popup_title : "Teoreettinen pilvien korkeus",
    cloudbase_popup_text : "Laskentatapa on yksinkertainen; 1000 jalkaa jokaista 4.4 Fahrenheit aste-<br><br>" +
                           "eroa lämpötilan ja kastepisteen välillä. Huomaa että tämä yksinkertaisesti<br>" +
                           "antaa vain teoreettisen korkeuden jossa kumpupilviä alkaisi muodostua,<br>" +
                           "ilman ollessa kylläinen",
    feet: "feet",
    metres: "metriä",
    //
    lowest_info : "Alin",
    highest_info : "Korkein",
    lowestF_info : "Alin",     // for proper translation of feminine words
    highestF_info : "Korkein",    // for proper translation of feminine words
    //
    RisingVeryRapidly : "Nousee erittäin nopeasti",
    RisingQuickly : "Nousee nopeasti",
    Rising : "Nousee",
    RisingSlowly : "Nousee hitaasti",
    Steady : "Vakaa",
    FallingSlowly : "Laskee hitaasti",
    Falling : "Laskee",
    FallingQuickly : "Laskee nopeasti",
    FallingVeryRapidly : "Laskee erittäin nopeasti",
    //
    maximum_info : "Korkein",
    max_hour_info : "Suurin sademäärä tunnissa",
    minimum_info : "Alin",
    //
    coords : ["P", "PKO", "KO", "IKO", "I", "IKA", "KA", "EKA", "E", "ELO", "LO", "LLO", "L", "LLU", "LU", "PLU"],
    compass : ["P", "KO", "I", "KA", "E", "LO", "L", "LU"],
    months : ["Tammikuuta", "Helmikuuta", "Maaliskuuta", "Huhtikuuta", "Toukokuuta", "Kesäkuuta", "Heinäkuuta", "Elokuuta", "Syyskuuta", "Lokakuuta", "Marraskuuta", "Joulukuuta"]
};

//======================================================================================================================
// Norwegian
//======================================================================================================================
LANG.NO = {
    canvasnosupport : "Ingen HTML5 Canvas-støtte i nettleseren din ... Beklager ...<br>" +
                      "Prøv å oppgradere nettleseren til en nyere versjon - nesten alle nettlesere støtter Canvas nå, selv IE9!<br><br>" +
                      "Omdirigerer deg til den 'gamle' målersiden ...",
    //
    led_title : "Fjernsensor: Status ukjent",
    led_title_ok : "Fjernsensor: OK",
    led_title_lost : "Fjernsensor: Mistet kontakt!",
    led_title_unknown : "Fjernsensor: Status ukjent!",
    led_title_offline: "Værstasjonen er ikke aktiv.",
    //
    weather   : "vær",
    latitude  : "Breddegrad",
    longitude : "Lengdegrad",
    elevation : "Høyde",
    //
    statusStr : "Laster...",
    StatusMsg : "Laster ned...",
    StatusHttp : "HTTP forespørsel mislyktes",
    StatusRetry : "Prøver igjen...",
    StatusRetryIn : "Prøver igjen om...",
    StatusTimeout : "Tidsavbrutt",
    StatusPageLimit : "Sidens automatiske oppdatering er nådd, du må oppdatere (F5) siden for å fortsette",
    //
    StatusLastUpdate : "Siste oppdatering",
    StatusMinsAgo : "minutter siden",
    StatusHoursAgo : "timer siden",
    StatusDaysAgo : "dager siden",
    //
    realtimeCorrupt : "Tekstfilnedlasting avbrutt! Prøver på nytt ...",
    //
    timer : "sekunder",
    at : "klokken",
    //
    // MAXIMUM number of characters that can be used for the 'title' variables (such as 'LANG_EN.temp_title_out')
    //        11 characters  ===="12345678901"====  11 characters
    //
    temp_title_out : "Utetemperatur",
    temp_title_in : "Innetemperatur",
    temp_out_info : "Utetemperatur",
    temp_out_web : "Utendørs",
    temp_in_info : "Innetemperatur",
    temp_in_web : "Innendørs",
    temp_trend_info : "Temperaturtrend",
    //
    dew_title : "Duggpunkt",
    dew_info : "Duggpunkt",
    dew_web : "Duggpunkt",
    apptemp_title : "Følt",
    apptemp_info : "Følt temperatur",
    apptemp_web : "Følt",
    chill_title : "Vindfaktor",
    chill_info : "Vindfaktor",
    chill_web : "Vindfaktor",
    heat_title : "Varmeindeks",
    heat_info : "Varmeindeks",
    heat_web : "Varmeindeks",
    humdx_title : "Luftfuktighet",
    humdx_info : "Luftfuktighet",
    humdx_web : "Luftfuktighet",
    //
    rain_title : "Nedbør",
    rrate_title : "Nedbør",
    rrate_info : "Nedbørsrate",
    LastRain_info : "Sist nedbør",
    LastRainedT_info : "I dag klokken",
    LastRainedY_info : "I går klokken",
    //
    hum_title_out : "Luftfuktighet",
    hum_title_in : "Luftfuktighet inne",
    hum_out_info : "Luftfuktighet ute",
    hum_in_info : "Luftfuktighet inne",
    hum_out_web : "Utendørs",
    hum_in_web : "Innendørs",
    //
    baro_title : "Trykk",
    baro_info : "Barometrisk trykk",
    baro_trend_info : "Trykktrend",
    //
    wind_title : "Vindhastighet",
    tenminavg_title : "Snitt vindhastighet",
    tenminavgwind_info : "Snitt vindhastighet (10 min)",
    maxavgwind_info : "Maks. snitt vindhastighet",
    tenmingust_info : "Vindkast (10 min)",
    maxgust_info : "Høyeste vindkast",
    latest_title : "Siste vind",
    latestwind_info : "Siste vindhastighet",
    bearing_info : "Retning",
    latest_web : "Siste",
    tenminavg_web : "Snitt",
    dominant_bearing : "Dominerende vind i dag",
    calm: "stille",
    windrose: "Vindrose",
    windruntoday: "Vindstrekning i dag",
    //
    uv_title : "UV Indeks",
    uv_levels : ["Ingen",
                 "Ingen fare",
                 "Liten risiko",
                 "Høy risiko",
                 "Veldig høy risiko",
                 "Ekstrem risiko"],
    uv_headlines : ["Ingen målbar UV Indeks",
                    'Ingen fare for en "gjennomsnittlig" person',
                    "Liten risiko for skade fra ubeskyttet soling",
                    "Høy risiko for skade fra ubeskyttet soling",
                    "Svært høy risiko for skade fra ubeskyttet soling",
                    "Ekstrem risiko for skade fra ubeskyttet soling"],
    uv_details : ["Det er fortsatt natt - eller det er en veldig overskyet dag.",

                 "Bruk solbriller på lyse dager, bruk solkrem hvis det er snø på bakken,<br>" +
                 "som reflekterer UV-stråling - hvis du ikke har spesielt god hud.",

                 "Bruk solbriller og SPF30 + solkrem, dekk kroppen med klær og<br>" +
                 "en lue, søk skygge midt på dagen når solen er mest intens.",

                 "Bruk solbriller og SPF30 + solkrem, dekk kroppen med solbeskyttende<br>" +
                 "klær og en skyggelue, reduser tid i solen fra to timer før<br>" +
                 "til tre timer etter at solen er på det høyeste (ca 11:00 til 16:00 om sommeren i<br>" +
                 "(soner som bruker sommertid).",

                 "Bruk SPF30 + solkrem, en skjorte, solbriller og solhatt.<br>" +
                 "Ikke være ute i solen for lenge.",

                 "Ta alle forholdsregler, inkludert: bruk av solbriller samt bruk SPF30 + solkrem,<br>" +
                 "dekk kroppen med en langermet skjorte og bukse, bruk skyggelue og<br>" +
                 "unngå solen fra to timer før til tre timer etter at solen er på det høyeste (ca 11:00<br>" +
                 "til 16:00 om sommeren i soner som bruker sommertid)."],
    //
    solar_title : "Solstråling",
    solar_currentMax : "Nåværende teoretiske maksimale måling",
    solar_ofMax : "av maksimalt",
    solar_maxToday : "Dagens maksimale måling",
    //
    cloudbase_title:"Skybase",
    cloudbase_popup_title : "Teoretisk skybase",
    cloudbase_popup_text : "Beregningen er en enkel en; 1000 feet for hver 4,4 grader Fahrenheit<br>" +
                           "forskjell mellom temperaturen og duggpunktet. Legg merke til at dette<br>" +
                           "gir bare den teoretiske høyden hvor cumulusskyer ville begynne å dannes, når<br>" +
                           "luften er mettet.",
    feet: "feet",
    metres: "meters",
    //
    lowest_info : "Lavest",
    highest_info : "Høyest",
    lowestF_info : "Lavest",     // for proper translation of feminine words
    highestF_info : "Høyest",    // for proper translation of feminine words
    //
    RisingVeryRapidly : "Øker veldig raskt",
    RisingQuickly : "Øker raskt",
    Rising : "Øker",
    RisingSlowly : "Øker sakte",
    Steady : "Jevn",
    FallingSlowly : "Faller sakte",
    Falling : "Faller",
    FallingQuickly : "Faller raskt",
    FallingVeryRapidly : "Faller veldig raskt",
    //
    maximum_info : "Maks.",
    max_hour_info : "Maks. pr. time",
    minimum_info : "Min.",
    //
    coords : ["N", "NNØ", "NØ", "ØNØ", "Ø", "ØSØ", "SØ", "SSØ", "S", "SSV", "SV", "VSV", "V", "VNV", "NV", "NNV"],
    compass : ["N", "NØ", "Ø", "SØ", "S", "SV", "V", "NV"],
    months : ["Jan", "Feb", "Mar", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Des"]
};

//======================================================================================================================
// Italian
//======================================================================================================================
LANG.IT = {
    canvasnosupport : "HTML5 Canvas non supportato dal tuo browser... Spiacente...<br>" +
                      "Prova ad aggiornare il tuo browser ad una versione pi&egrave; recente - quasi tutti i browser supportano Canvas adesso, eccetto IE9!<br><br>" +
                      "Ti sto reindirizzando ad una pagina dei calibri precedente...",
    //
    led_title          : "Sensori: stato sconosciuto",
    led_title_ok       : "Sensori: comumicazione OK",
    led_title_lost     : "Sensori: comunicazione non OK",
    led_title_unknown  : "Sensori: stato sconosciuto",
    led_title_offline  : "Stazione meteo temporaneamente OFFLINE",
    //
    weather   : "meteo",
    latitude  : "Latitudine",
    longitude : "Longitudine",
    elevation : "Altezza",
    //
    statusStr : "Caricamento...",
    StatusMsg : "Downloading...",
    StatusHttp : "Richiesta HTTP Fallita",
    StatusRetry : "Sto riprovando...",
    StatusRetryIn : "Riprovo in...",
    StatusTimeout : "Timed out",
    StatusPageLimit : "Raggiunto limite auto-aggiornamento pagina, aggiorna la pagina per continuare",
    //
    StatusLastUpdate : "Ultimo aggiornamento",
    StatusMinsAgo : "minuti fa",
    StatusHoursAgo : "ore fa",
    StatusDaysAgo : "giorni fa",
    //
    realtimeCorrupt : "File di testo scaricato corrotto! Sto riprovando...",
    //
    timer : "secondi",
    at : "alle",
    //
    // MAXIMUM number of characters that can be used for the 'title' variables (such as 'LANG_EN.temp_title_out')
    //        11 characters  ===="12345678901"====  11 characters
    //
    //
    temp_title_out     : "Temp. est.",
    temp_title_in      : "Temp. int.",
    temp_out_info      : "Temp. esterna",
    temp_out_web       : "Esterna",
    temp_in_info       : "Temp. interna",
    temp_in_web        : "Interna",
    temp_trend_info    : "Variaz. temp.",
    //
    dew_title          : "Dew Point",
    dew_info           : "Dew Point",
    dew_web            : "Dew Point",
    apptemp_title      : "T. apparen.",
    apptemp_info       : "Temp. apparente",
    apptemp_web        : "Temp. apparente",
    chill_title        : "Wind Chill",
    chill_info         : "Wind Chill",
    chill_web          : "Wind Chill",
    heat_title         : "Ind. calor.",
    heat_info          : "Indice di calore",
    heat_web           : "Indice di calore",
    humdx_title        : "Ind. umid.",
    humdx_info         : "Indice di umidità",
    humdx_web          : "Indice di umidità",
    //
    rain_title         : "Precipitaz.",
    rrate_title        : "Rain Rate",
    rrate_info         : "Rateo precipitaz.",
    LastRain_info      : "Ultime precipit.",
    LastRainedT_info   : "Oggi alle",
    LastRainedY_info   : "Ieri alle",
    //
    hum_title_out      : "Umidità",
    hum_title_in       : "Umid. int.",
    hum_out_info       : "Umid. esterna",
    hum_in_info        : "Umid. interna",
    hum_out_web        : "Esterna",
    hum_in_web         : "Interna",
    //
    baro_title         : "Pressione",
    baro_info          : "Pressione barometrica",
    baro_trend_info    : "Variaz. press.",
    //
    wind_title         : "Vel. vento",
    tenminavg_title    : "Vel. media",
    tenminavgwind_info : "Vel. media vento (10 min. media)",
    maxavgwind_info    : "Vel. max. vento (10 min. media)",
    tenmingust_info    : "Raffica max. (ultimi 10 min.)",
    maxgust_info       : "Raffica max.",
    latest_title       : "Ult. mis.",
    latestwind_info    : "Ultima vel. misurata",
    bearing_info       : "Direzione",
    latest_web         : "Ultima direz.",
    tenminavg_web      : "Direz. media",
    dominant_bearing   : "Vento dominante oggi",
    calm               : "calmo",
    windrose           : "Direz. dominante",
    windruntoday       : "Wind run odierno",
    //
    uv_title : "Indice UV",
    uv_levels : ["Nessuna",
                 "Nessun pericolo",
                 "Rischio basso",
                 "Rischio alto",
                 "Rischio molto alto",
                 "Rischio estremo"],
    uv_headlines : ["Indice UV non misurabile",
                    "Nessun pericolo per la maggior parte delle persone",
                    "Lieve pericolo di scottatura da una esposizione al sole non adeguatamente protetta",
                    "Alto rischio di scottature da una esposizione al sole non adeguatamente protetta",
                    "Rischio molto alto di scottature da una esposizione al sole non adeguatamente protetta",
                    "Rischio estremo di scottature da una esposizione al sole non adeguatamente protetta"],
    uv_details : ["E' ancora notte o è molto nuvoloso.",

                 "Indossa occhiali da sole nei giorni con molta luce; usa lenti da sole con neve al suolo,<br>" +
                 "che potrebbe riflettere radiazioni UV, o se hai pelle particolarmente chiara.",

                 "Indossa occhiali da sole ed utilizza protezioni solari SPF 30+, mantieni i vestiti ed indossa<br>" +
                 "un cappello, riparati all'ombra durante le ore centrali quando il sole è più intenso.",

                 "Indossa occhiali da sole ed utilizza protezioni solari SPF 30+, proteggi il corpo indossando vestiti chiari<br>" +
                 "e un cappello a tesa larga, riduci inoltre l'esposizione al sole da due ore prima<br>" +
                 "a tre ore dopo mezzogiorno (approssimativamente dalle 11:00 alle 16:00 durante l'estate nelle<br>" +
                 "zone che osservano l'ora legale).",

                 "Utilizza protezioni solari SPF 30+, una t-shirt, occhiali da sole, ed un cappello.<br>" +
                 "Non stare al sole troppo a lungo.",

                 "Prendi tutte le precauzioni, incluso: occhiali da sole, utilizzo protezioni solari SPF 30+,<br>" +
                 "proteggi il corpo con una t-shirt a maniche lunghe e pantaloni, indossa un cappello con tesa, ed<br>" +
                 "evita il sole da due ore prima a tre ore dopo mezzogiorno (approssimativamente dalle 11:00<br>" +
                 "alle 16:00 durante l'estate nelle zone che osservano l'ora legale)."],
    //
    solar_title : "Radiazione solare",
    solar_currentMax : "Attuale massima lettura teorica",
    solar_ofMax : "del massimo",
    solar_maxToday : "Lettura massima odierna",
    //
    cloudbase_title: "Base nuvola",
    cloudbase_popup_title: "base nuvola teorica",
    cloudbase_popup_text: "Il calcolo è semplice; 1000 piedi per ogni 4,4 gradi Fahrenheit <br>" +
                          "differenza tra la temperatura e il punto di rugiada. Si noti che questo semplicemente <br> " +
                          "dà l'altezza teorica a cui Cumulus nuvole avrebbero cominciato a formare, <br>" +
                          "perché l'aria essere satura",
    feet: "piedi",
    metres: "metri",
    //
    lowest_info : "Minimo",
    highest_info : "Massimo",
    lowestF_info : "Minima",      // for proper translation of feminine words
    highestF_info : "Massima",    // for proper translation of feminine words
    //
    RisingVeryRapidly : "In crescita molto rapida",
    RisingQuickly : "In crescita rapida",
    Rising : "In crescita",
    RisingSlowly : "In crescita lenta",
    Steady : "Costante",
    FallingSlowly : "In lento calo",
    Falling : "In calo",
    FallingQuickly : "In veloce calo",
    FallingVeryRapidly : "In calo molto rapido",
    //
    maximum_info : "Massimo",
    max_hour_info : "Max. per ora",
    minimum_info : "Minimo",
    //
    coords : ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSO", "SO", "OSO", "O", "ONO", "NO", "NNO"],
    compass : ["N", "NE", "E", "SE", "S", "SO", "O", "NO"],
    months : ["Gen", "Feb", "Mar", "Apr", "Mag", "Giu", "Lug", "Ago", "Set", "Ott", "Nov", "Dic"]
};

//======================================================================================================================
// Spanish
// ***INCOMPLETE***
//======================================================================================================================
LANG.ES = {
    canvasnosupport : "No hay soporte para HTML5 en su navegador ... Lo siento ...<br>" +
                      "Trate de actualizar su navegador a una versión más reciente - casi todos los navegadores son compatibles con él ahora, incluso IE9!<br><br>" +
                      "Redirigir a la antigua página de medidores ...",
    //
    led_title : "Sensor remoto en estado desconocido",
    led_title_ok : "Sensor remoto funcionando correctamente",
    led_title_lost : "Contacto perdido con el sensor remoto",
    led_title_unknown : "Sensor remoto en estado desconocido",
    led_title_offline : "La estación meteorológica no está conectada",
    //
    weather : "estado del tiempo",
    latitude : "Latitud",
    longitude : "Longitud",
    elevation : "Elevación",
    //
    statusStr : "Cargando ...",
    StatusMsg : "Recibiendo ...",
    StatusHttp : "Error en petición HTTP",
    StatusRetry : "Volviendo a probar ...",
    StatusRetryIn : "Reintentando ...",
    StatusTimeout : "Tiempo excedido",
    StatusPageLimit : "Se ha alcanzado el limite de tiempo de actualizacion automatica, recargue la pagina para continuar",
    //
    StatusLastUpdate : "Ultima actualización a las",
    StatusMinsAgo : "minutos",
    StatusHoursAgo : "horas",
    StatusDaysAgo : "días",
    //
    realtimeCorrupt : "Archivo dañado! Reintentando ...",
    //
    timer : "Segundos",
    at : "a las",
    // MAXIMUM number of characters that can be used for the 'title' variables (such as 'LANG_EN.temp_title_out')
    //        11 characters  ===="12345678901"====  11 characters
    //
    temp_title_out : "Temperatura",
    temp_title_in : "Temp. interior",
    temp_out_info : "Temperatura exterior",
    temp_out_web : "Exterior",
    temp_in_info : "Temperatura interior",
    temp_in_web : "Interior",
    temp_trend_info : "Tendencia",
    //
    dew_title : "Rocío",
    dew_info : "Temperatura de rocío",
    dew_web : "Rocío",
    apptemp_title : "Temp. aparente",
    apptemp_info : "Temperatura aparente",
    apptemp_web : "Temperatura aparente",
    chill_title : "Windchill",
    chill_info : "Windchill",
    chill_web : "Windchill",
    heat_title : "Indice de calor",
    heat_info : "Indice de calor",
    heat_web : "Indice de calor",
    humdx_title : "Humidex",
    humdx_info : "Humidex",
    humdx_web : "Humidex",
    //
    rain_title : "Precipitación",
    rrate_title : "Intensidad de lluvia",
    rrate_info : "Intensidad de lluvia",
    LastRain_info : "Llovió por última vez",
    LastRainedT_info : "Hoy a las",
    LastRainedY_info : "Ayer a las",
    //
    hum_title_out : "Humedad",
    hum_title_in : "Humedad interior",
    hum_out_info : "Humedad Exterior",
    hum_in_info : "Humedad interior",
    hum_out_web : "Exterior",
    hum_in_web : "Interior",
    //
    baro_title : "Presión",
    baro_info : "Presión",
    baro_trend_info : "Tendencia de presión",
    //
    wind_title : "Velocidad Viento",
    tenminavg_title : "Velocidad media actual",
    tenminavgwind_info : "Velocidad media actual (10 min)",
    maxavgwind_info : "Velocidad media máxima",
    tenmingust_info : "Rácha (10 min)",
    maxgust_info : "Racha máxima",
    latest_title : "Viento actual",
    latestwind_info : "Última lectura de viento",
    bearing_info : "Dirección",
    latest_web : "Actual",
    tenminavg_web : "Media",
    dominant_bearing : "Viento dominante hoy",
    calm : "calma",
    windrose : "Rosa de los Vientos",
    windruntoday : "Recorrido del viento hoy",
    //
    uv_title : "Indice UV",
    uv_levels : ["Nulo",
                 "Sin riesgo",
                 "Riesgo bajo",
                 "Riesgo alto",
                 "Riesgo muy alto",
                 "Riesgo extremo"],
    uv_headlines : ["Indice UV no medible",
                    "Sin riesgo para la población media",
                    "Riesgo bajo de quemadura por exposición al sol sin protección",
                    "Riesgo alto de quemadura por exposición al sol sin protección",
                    "Riesgo muy alto de quemadura por exposición al sol sin protección",
                    "Riesgo extremo de quemadura por exposición al sol sin protección"],
    uv_details : ["Es aun de noche o el día está muy nublado.",

                  "Use gafas de sol en días soleados; use protección solar si hay nieve en el suelo,<br>" +
                  "que refleja la radiación UV, o si tiene la piel particularmente sensible.",

                  "Use gafas de sol y protector solar factor 30 o superior, cubra el cuerpo con ropa y<br>" +
                  "una gorra, y busque zonas de sombra al mediodía cuando el sol es más intenso.",

                  "Use gafas de sol y protección solar factor 30 o superior, cubra el cuerpo con ropas que protejan del sol<br>" +
                  "y sombreros anchos, y reduzca el tiempo al sol desde dos horas antes hasta<br>" +
                  "tres horas después del mediodía solar (aproximadamente desde las 11:00 hasta las 16:00 en verano).",

                  "Use protección solar factor 30 o superior, camisa, gafas de sol, y gorra.<br>" +
                  "No permanezca al sol durante mucho tiempo.",

                  "Tome todas las precauciones, incluyendo: usar gafas de sol y protección solar factor 30 o superior,<br>" +
                  "cubrir el cuerpo con camisas de manga larga y pantalones, llevar sombreros de ala ancha, y<br>" +
                  "evitar el sol desde dos horas antes hasta tres horas después del mediodía solar<br>" +
                  "(aproximadamente desde las 11:00 hasta las 16:00 PM en verano)."],
    //
    solar_title : "Radiación solar",
    solar_currentMax : "Máximo teórico en este momento",
    solar_ofMax : "del máximo teórico",
    solar_maxToday : "Valor máximo real hoy",
    //
    cloudbase_title : "base de las nubes",
    cloudbase_popup_title : "Theoretical cloud base",
    cloudbase_popup_text : "The calculation is a simple one; 1000 feet for every 4.4 degrees Fahrenheit<br>" +
                           "difference between the temperature and the dew point. Note that this simply<br>" +
                           "gives the theoretical height at which Cumulus clouds would begin to form, the<br>" +
                           "air being saturated",
    feet: "feet",
    metres: "metros",
    //
    lowest_info : "Mínima",
    highest_info : "Máxima",
    lowestF_info : "Mínima",
    highestF_info : "Máxima",
    //
    RisingVeryRapidly : "Subiendo muy rápidamente",
    RisingQuickly : "Subiendo rápidamente",
    Rising : "Subiendo",
    RisingSlowly : "Subiendo lentamente",
    Steady : "Estable",
    FallingSlowly : "Bajando lentamente",
    Falling : "Bajando",
    FallingQuickly : "Bajando rápidamente",
    FallingVeryRapidly : "Bajando muy rápidamente",
    //
    maximum_info : "Máxima",
    max_hour_info : "Máxima por hora",
    minimum_info : "Mínima",
    //
    coords : ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "OSO", "O", "OSO", "O", "ONO", "NO", "NNO", "ONO"],
    compass : ["N", "NE", "E", "SE", "S", "SO", "O", "NO"],
    months : ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"]
};

//======================================================================================================================
// Catalan
// ***INCOMPLETE***
//======================================================================================================================
LANG.CT = {
    canvasnosupport : "No hi ha suport per HTML5 en el seu navegador ... Ho sento ...<br>" +
                      "Intenta actualitzar el vostre navegador a una versió més recent - gairebé tots els navegadors són compatibles amb HTML5 ara, fins i tot IE9!<br><br>" +
                      "Redirigir a una 'vella' pàgina de mesuradors ...",
    //
    led_title : "Estat del sensor remot desconegut",
    led_title_ok : "Sensor remot correcte",
    led_title_lost : "Perdut contacte amb el sensor remot",
    led_title_unknown : "Estat del sensor remot desconegut ",
    led_title_offline : "L'estació meteorològica està fora de línea",
    //
    weather   : "El Temps",
    latitude  : "Latitud",
    longitude : "Longuitud",
    elevation : "Elevació",
    //
    statusStr : "Carregant ...",
    StatusMsg : "Descàrrega de dades ...",
    StatusHttp : "Petició HTTP Error",
    StatusRetry : "Tornant a provar ...",
    StatusRetryIn : "Tornant a provar en ..",
    StatusTimeout : "Temps d'espera esgotat",
    StatusPageLimit : "La autoactualització de la pàgina ha arribat al límit, actualitza el teu navegador per a continuar",
    //
    StatusLastUpdate : "Última actualització",
    StatusMinsAgo : "minuts",
    StatusHoursAgo : "hores",
    StatusDaysAgo : "dies",
    //
    realtimeCorrupt : "Descàrrega de l'arxiu de text corrupte! reintentant ...",
    //
    timer : "segons",
    at : "a les ",
    //
    // MAXIMUM number of characters that can be used for the 'title' variables (such as 'LANG_EN.temp_title_out')
    //        11 characters  ===="12345678901"====  11 characters
    //
    temp_title_out : "Temperatura",
    temp_title_in : "°T Interior",
    temp_out_info : "Temp exterior",
    temp_out_web : "Exterior",
    temp_in_info : "Temp interior",
    temp_in_web : "Interior",
    temp_trend_info : "Tendència",
    //
    dew_title : "Rosada",
    dew_info : "Punt de rosada",
    dew_web : "Punt de rosada",
    apptemp_title : "Temp aparent",
    apptemp_info : "Temperatura aparent",
    apptemp_web : "Aparent",
    chill_title : "°T sensació",
    chill_info : "°T sensació",
    chill_web : "°T sensació",
    heat_title : "Índex de calor",
    heat_info : "Índex de calor",
    heat_web : "Índex de calor",
    humdx_title : "°T xafogor",
    humdx_info : "°T xafogor",
    humdx_web : "°T xafogor",
    //
    rain_title : "Pluja",
    rrate_title : "Intensitat de pluja",
    rrate_info : "Intensitat de pluja",
    LastRain_info : "Últim dia de pluja",
    LastRainedT_info : "Avui a les",
    LastRainedY_info : "Ahir a les",
    //
    hum_title_out : "Humitat Exterior",
    hum_title_in : "Humitat Interior",
    hum_out_info : "Humitat Exterior",
    hum_in_info : "Humitat Interior",
    hum_out_web : "Exterior",
    hum_in_web : "Interior",
    //
    baro_title : "Pressió",
    baro_info : "Pressió",
    baro_trend_info : "Tendència",
    //
    wind_title : "Velocitat del Vent",
    tenminavg_title : "Velocitat mitjana del vent",
    tenminavgwind_info : " Vel. del Vent (promig 10')",
    maxavgwind_info : "Vent Màxim (promig 10')",
    tenmingust_info : "Ràfega màxima (últims 10 min)",
    maxgust_info : "Ràfega màxima",
    latest_title : "Direcció del vent",
    latestwind_info : "Últimes velocitat del vent",
    bearing_info : "última",
    latest_web : "Darrer",
    tenminavg_web : "Mitjana",
    dominant_bearing : "Vent dominant avui",
    calm: "calma",
    windrose: "Rosa dels Vents",
    windruntoday: "Recorregut del vent avui",
    //
    uv_title : "Index UV",
    uv_levels : ["Cap",
                 "Sense perill",
                 "Risc mínim",
                 "Risc alt",
                 "Risc molt alt",
                 "Risc extrem"],
    uv_headlines : ["Index UV no mesurable",
                    "Sense perill per a la mitjana de les persones",
                    "Risc baix de lesions per exposició al sol sense protecció",
                    "Risc alt de lesions per exposició al sol sense protecció",
                    "Risc molt alt de lesions per exposició al sol sense protecció",
                    "Risc extrem de lesions per exposició al sol sense protecció"],
    uv_details : ["Encara és de nit o és un dia molt tapat.",

                 "Utilitza ulleres de sol en dies clars; feu servir protecció solar si hi ha neu al terra,<br>" +
                 "que reflecteix la radiació UV, o si té la pell particularment clara.",

                 "Utilitzar ulleres de sol i protecció solar SPF 30+, cobrir el cos amb roba i<br>" +
                 "barret, busqui ombra per migdia quan el sol és més intens.",

                 "Utilitzi ulleres de sol i protecció solar SPF 30+, cobriu el cos amb roba<br>" +
                 "i un barret, reduiu l'exposició al sol de dos<br>" +
                 "a tres hores abans del migdia solar (normalment de 11:00 AM a 4:00 PM durant l'estiu en <br>" +
                 "zones que fan servir l'horari d'estiu).",

                 "Utilitzar protecció solar SPF 30+, una camisa, ulleres de sol i barret.<br>" +
                 "No estar al sol durant llargs períodes de temps.",

                 "Prengui totes les precaucions, incloent: utilitzar ulleres de sol i protecció solar SPF 30+,<br>" +
                 "cobrir el cos amb camises de màniga llarga i pantalons llargs, utilitzar un barret ampli, i<br>" +
                 "evitar el sol a partir de dues hores abans o tres hores després del migdia solar (aprox. de 11:00 AM<br>" +
                 "a 4:00 PM durant l'estiu en zones que fan servir l'horari d'estiu)."],
    //
    solar_title : "Radiació Solar",
    solar_currentMax : "Màxima lectura teòrica actual",
    solar_ofMax : "del màxim",
    solar_maxToday : "Lectura màxima d'avui",
    //
    cloudbase_title : "base dels núvols",
    cloudbase_popup_title : "Theoretical cloud base",
    cloudbase_popup_text : "The calculation is a simple one; 1000 feet for every 4.4 degrees Fahrenheit<br>" +
                           "difference between the temperature and the dew point. Note that this simply<br>" +
                           "gives the theoretical height at which Cumulus clouds would begin to form, the<br>" +
                           "air being saturated",
    feet: "feet",
    metres: "metres",
    //
    lowest_info : "Mínima",
    highest_info : "Màxima",
    lowestF_info : "Mínima",
    highestF_info : "Màxima",
    //
    RisingVeryRapidly : "Pujant molt ràpidament",
    RisingQuickly : "Pujant ràpidament",
    Rising : "Pujant",
    RisingSlowly : "Pujant lentament",
    Steady : "Estable",
    FallingSlowly : "Baixant lentament",
    Falling : "Baixant",
    FallingQuickly : "Baixant ràpidament",
    FallingVeryRapidly : "Baixant molt ràpidament",
    //
    maximum_info : "Màxim",
    max_hour_info : "Pluja horària màxima",
    minimum_info : "Mínim",
    //
    coords : ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "WSW", "W", "WSW", "W", "WNW", "NW", "NNW", "WNW"],
    compass : ["N", "NE", "E", "ES", "S", "SW", "W", "NW"],
    months : ["Gener", "Febrer", "Març", "Abril", "Maig", "Juny", "Juliol", "Agost", "Setembre", "Octubre", "Novembre", "Desembre"]
};

//======================================================================================================================
// Greek by Dimitris Vichos
//======================================================================================================================
LANG.GR = {
    canvasnosupport : "Η έκδοση του browser που χρησιμοποιήτε δεν υποστηρίζει την γλώσσα HTML-5<br>" +
                      "Αναβαθμίστε τον browser που χρησιμοποιήτε σε εκδοση που υποστηρίζει HTML-5 <br><br>" +
                      "Ανακατεύθνση σελίδας στην παλιά.. gauges page...",
    //
    led_title : "Απώλεια επαφής με τον αισθητήρα",
    led_title_ok : "Αισθητήρα εντάξει",
    led_title_lost : "Απώλεια επαφής!",
    led_title_unknown : "Αγωστη κατάσταση αισθητήρα!",
    led_title_offline: "Ο μετεωρολογικός σταθμός είναι εκτός δικτύου.",
    //
    weather : "Καιρός",
    latitude : "Βόρειο πλάτος",
    longitude : "Ανατολικό μήκος",
    elevation : "Υψόμετρο",
    //
    statusStr : "Φορτώνει...",
    StatusMsg : "Φότρωση δεδομένων....",
    StatusHttp : "Απώλεια διεύθυνσης http...",
    StatusRetry : "Επανάληψη...",
    StatusRetryIn : "Επανάληψη σε...",
    StatusTimeout : "Λήξη χρόνου",
    StatusPageLimit : "Παρακαλώ ανανεώστε τον browser....",
    //
    StatusLastUpdate : "Τελευταία λήψη δεδομένων",
    StatusMinsAgo : "λεπτά πρίν",
    StatusHoursAgo : "ώρες πρίν",
    StatusDaysAgo : "μέρες πριν",
    //
    realtimeCorrupt : "Προσπάθεια ανάκτησης δεδομένων, επανάληψη",
    //
    timer : "Δευτερόλεπτα",
    at : "στις",
    //
    // MAXIMUM number of characters that can be used for the 'title' variables (such as 'LANG_EN.temp_title_out')
    // 11 characters ===="12345678901"==== 11 characters
    //
    temp_title_out : "Θερμόμετρο",
    temp_title_in : "Εσωτερική θερμοκρασία",
    temp_out_info : "Εξωτερική θερμοκρασία",
    temp_out_web : "Εξωτερική",
    temp_in_info : "Εσωτερική θερμοκρασία",
    temp_in_web : "Εσωτερική",
    temp_trend_info : "Τάση θερμοκρασίας",
    //
    dew_title : "Σημείο δρόσου",
    dew_info : "Σημείο δρόσου",
    dew_web : "Σημείο δρόσου",
    apptemp_title : "Αισθητή θερμοκρασία",
    apptemp_info : "Αισθητή θερμοκρασία",
    apptemp_web : "Αισθητή θερμοκρασία",
    chill_title : "Αίσθηση ψύχους",
    chill_info : "Αίσθηση ψύχους",
    chill_web : "Αίσθηση ψύχους",
    heat_title : "Δείκτης δυσφορίας",
    heat_info : "Δείκτης δυσφορίας",
    heat_web : "Δείκτης δυσφορίας",
    humdx_title : "Δείκτης υγρασίας",
    humdx_info : "Δείκτης υγρασίας",
    humdx_web : "Δείκτης υγρασίας",
    //
    rain_title : "Βροχόπτωση",
    rrate_title : "Ενταση βροχόπτωσης",
    rrate_info : "Ενταση βροχόπτωσης",
    LastRain_info : "Τελευταία βροχόπτωση",
    LastRainedT_info : "Σήμερα στις",
    LastRainedY_info : "χθες στις",
    //
    hum_title_out : "Υγρασία",
    hum_title_in : "Εσωτερική υγρασία",
    hum_out_info : "Εξωτερική υγρασία",
    hum_in_info : "Εσωτερική υγρασία",
    hum_out_web : "Εξωτερική",
    hum_in_web : "Εσωτερική",
    //
    baro_title : "Βαρόμετρο",
    baro_info : "Ατμοσφαιρική πίεση",
    baro_trend_info : "Τάση πίεσης",
    //
    wind_title : "Aνεμόμετρο",
    tenminavg_title : "Μέση ταχύτητα ανέμου",
    tenminavgwind_info : "Μέση 10λεπτη ταχύτητα ανέμου",
    maxavgwind_info : "Μέγιστη μέση ταχύτητα ανέμου",
    tenmingust_info : "Ριπή ανέμου",
    maxgust_info : "Μέγιστη ριπή ανέμου",
    latest_title : "Τελευταία ένδειξη ανέμου",
    latestwind_info : "Τελευταία ταχύτητα ανέμου",
    bearing_info : "Διέυθυνση",
    latest_web : "Τελευταία",
    tenminavg_web : "Μέση",
    dominant_bearing : "Επικρατέστερη διέυθυνση",
    calm: "Νηνεμία",
    windrose: "Ανεμούριο",
    windruntoday: "Τρέξιμο Αέρα",
    //
    uv_title : "UV Ενδειξη",
    uv_levels : ["Μηδενικό",
                 "Ακίνδυνο",
                 "Μικρή Επικυνδυνότητα",
                 "Υψηλή Επικυνδυνότητα",
                 "Πολύ υψηλή επικυνδυνότητα",
                 "Ακραία επικυνδυνότητα"],
    uv_headlines : ["Μη μετρήσιμη UV ένδεικη",
                    "Ακίνδυνη στο μέσο επίπεδο",
                    "Μικρός κίνδυνος στην ηλιακή ακτινοβολία",
                    "Υψηλός κίνδυνος στην ηλιακή ακτινοβολία",
                    "Πολύ υψηλός κίνδυνος στην ηλιακή ακτινοβολία",
                    "Eξαιρετικά υψηλός κίνδυνος στην ηλιακή ακτινοβολία"],
    uv_details : ["Ακόμα είναι νύχτα ή η μέρα είναι πολύ συννεφιασμένη.",

                  "Φοράμε γυαλιά ηλίου; φοράμε γυαλιά ηλίου όταν το έδαφος είναι χιονοσκεπές,<br>" +
                  "φοράμε ρούχα που αντανακλούν την ηλιακή UV ακτινοβολία.",

                  "Φοράμε γυαλιά και αντηλιακό με δείκτη SPF 30+ , Ντυνόμαστε με ανοιχτόχρωμα ρούχα<br>" +
                  "καπέλο, και αποφεύγουμε τον ήλιο τις θερμές ώρες της ημέρας.",

                  "Φοράμε γυαλιά και αντηλιακό με δείκτη SPF 30+ , καλύπτουμε το σώμα με αντηλιακό<br>" +
                  "Προγραμματίζουμε τις εξωτερικές δουλειές <br>" +
                  "3 ώρες μετά την μέγιστη ακτινοβολία (από τις 11 το πρωί ως τις 4 το αποευμα<br>" +
                  "Οι ζώνες της θερινής ώρας).",

                  "Φοράμε αντηλιακό με δείκτη SPF 30+ , Μπλουζάκι ανοιχτόχρωμο γυαλιά ηλίου και καπέλο.<br>" +
                  "Απόφέυγουμε την μακροχρόνια παραμονή στον ήλιο.",

                  "Παίρνουμς όλες τις προφυλάξεις, συμπεριλαμβανομένων: γυαλιά ηλίου και αντηλιακό με δείκτη SPF 30+,<br>" +
                  "Ντυνόμαστε με ανοιχτόχρωμα ρούχα ,καπέλο, και<br>" +
                  "Αποφεύγουμε τελείως τον ήλιο 2 ώρες πρίν και τρείς ώρες μετά το μέγιστο της ηλιακής ακτινοβολίας (από τις 11 το πρωί<br>" +
                  "ως τις 4 το απόγευμα )."],
    //
    solar_title : "Ηλιακή ακτινοβολία",
    solar_currentMax : "Τρέχουσα μέγιστη θεωρητικά ένδειξη",
    solar_ofMax : "της μέγιστης",
    solar_maxToday : "Σημερινή μέγιστη ένδειξη",
    //
    cloudbase_title : "Υψος βάσης νεφών",
    cloudbase_popup_title : "Θεωρητικό ύψος βάσης νεφών",
    cloudbase_popup_text :  "Ο υπολογισμός είναι απλός; 1000 πόδια για κάθε 4.4 Βαθμούς Φαρενάιτ.<br>" +
                            "Είναι η διαφορά μεταξύ της θερμοκρασίας και του σημείου δρόσου. Αυτή είναι απλή<br>" +
                            "Δίνει το θεωρητικό ύψος των νεφών στη βάση τους, <br>" +
                            "οταν ο αέρας είναι κορεσμένος",
    feet: "πόδια",
    metres: "μέτρα",
    //
    lowest_info : "Eλάχιστη",
    highest_info : "Μέγιστη",
    lowestF_info : "Ελάχιστη", // for proper translation of feminine words
    highestF_info : "Μέγιστη", // for proper translation of feminine words
    //
    RisingVeryRapidly : "Ραγδαία πτώση",
    RisingQuickly : "ταχεία πτώση",
    Rising : "πτώση",
    RisingSlowly : "Αργή πτώση",
    Steady : "Σταθερή",
    FallingSlowly : "Αργή άνοδος",
    Falling : "Ανοδος",
    FallingQuickly : "Ταχεία πτώση",
    FallingVeryRapidly : "Ραγδαία πτώση",
    //
    maximum_info : "Mέγιστη",
    max_hour_info : "Mέγιστη ωριαία",
    minimum_info : "Ελάχιστη",
    //
    coords : ["Β", "ΒΒΑ", "ΒΑ", "ΑΒΑ", "Α", "ΑΝΑ", "ΝΑ", "ΝΝΑ", "Ν", "ΝΝΔ", "ΝΔ", "ΝΔΝ", "Δ", "ΔΒΔ", "ΒΔ", "ΒΒΔ"],
    compass : ["Β", "ΒΑ", "Α", "ΝΑ", "Ν", "ΝΔ", "Δ", "ΒΔ"],
    months : ["Ιαν", "Φεβ", "Μαρ", "Απρ", "Μαι", "Ιουν", "Ιουλ", "Αυγ", "Σεπ", "Οκτ", "Νοε", "Δεκ"]
};

//======================================================================================================================
// Portuguese by Werk_AG, MeteoCercal.info
//======================================================================================================================
LANG.PT = {
    canvasnosupport:"O seu browser não suporta HTML5...<br>" +
                     "Actualize o seu navegador para uma versão mais recente!<br><br>" +
                     "Vai ser redirecionado para uma página compativel...",
    //
    led_title:"Sensor remoto, estado desconhecido",
    led_title_ok:"Aceitar sensor remoto",
    led_title_lost:"Perdido o contacto com o sensor remoto",
    led_title_unknown:"Sensor remoto em estado desconhecido",
    led_title_offline:"A estação metereológica está Offline",
    //
    weather:"clima",
    latitude:"Latitude",
    longitude:"Longitude",
    elevation:"Altitude",
    //
    statusStr:"A carregar ...",
    StatusMsg:"Descarga de ...",
    StatusHttp:"Erro no pedido HTTP",
    StatusRetry:"Voltando a tentar...",
    StatusRetryIn:"Voltando a tentar em ...",
    StatusTimeout:"Tempo esgotado",
    StatusPageLimit:"Foi atingido o tempo limite para auto-actualização desta página, recarregue a página para continuar.",
    //
    StatusLastUpdate:"Última actualização",
    StatusMinsAgo:"minutos atrás",
    StatusHoursAgo:"horas atrás",
    StatusDaysAgo:"dias atrás",
    //
    realtimeCorrupt:"Ficheiro de dados corrompido! Tentando de novo...",
    //
    timer:"Segundos",
    at:"a",
    //
    temp_title_out:"Temperatura",
    temp_title_in:"Temp. Interior",
    temp_out_info:"Temperatura Exterior",
    temp_out_web:"Exterior",
    temp_in_info:"Temperatura Interior",
    temp_in_web:"Interior",
    temp_trend_info:"Tendência Temperatura",
    //
    dew_title:"Pt. Orvalho",
    dew_info:"Ponto de Orvalho",
    dew_web:"Ponto de Orvalho",
    apptemp_title:"Temp. Aparente",
    apptemp_info:"Temperatura Aparente",
    apptemp_web:"Temp. Aparente",
    chill_title:"Índice Frio",
    chill_info:"Índice Frio",
    chill_web:"Índice Frio",
    heat_title:"Índice de Calor",
    heat_info:"Índice de Calor",
    heat_web:"Índice de Calor",
    humdx_title:"Humidex",
    humdx_info:"Humidex",
    humdx_web:"Humidex",
    //
    rain_title:"Pluviosidade",
    rrate_title:"Taxa Pluv.",
    rrate_info:"Taxa Pluviosidade",
    LastRain_info:"Pluviosidade",
    LastRainedT_info:"Hoje às",
    LastRainedY_info:"Ontem às",
    //
    hum_title_out:"Humidade",
    hum_title_in:"Humidade Interior",
    hum_out_info:"Humidade Exterior",
    hum_in_info:"Humidade Interior",
    hum_out_web:"Exterior",
    hum_in_web:"Interior",
    //
    baro_title:"Pressão Atm.",
    baro_info:"Pressão",
    baro_trend_info:"Tendência da Pressão",
    //
    wind_title:"Vel. Vento",
    tenminavg_title:"Velocidade Média do Vento",
    tenminavgwind_info:"Velocidade Média do Vento (10 min)",
    maxavgwind_info:"Média da Vel. Máxima do Vento",
    tenmingust_info:"Rajada (10 min)",
    maxgust_info:"Rajada máxima",
    latest_title:"Vento, última",
    latestwind_info:"Última Vel. do Vento",
    bearing_info:"Direção",
    latest_web:"Último",
    tenminavg_web:"Média",
    dominant_bearing:"Vento dominante hoje",
    calm:"Calmo",
    windrose:"Rosa dos Ventos",
    windruntoday:"Wind Run hoje",
    //
    uv_title:"Índice UV",
    uv_levels:["Nenhum",
               "Sem perigo",
               "Pequeno risco",
               "Alto risco",
               "Risco muito alto",
               "Risco extremo"],
    uv_headlines:["Índice UV não mensurável",
                  "Sem perigo para a maioria das pessoas",
                  "Pequeno risco de danos por exposição ao sol sem protecção",
                  "Alto risco de danos por exposição ao sol sem protecção",
                  "Risco muito alto de danos por exposição ao sol sem protecção",
                  "Risco extremo de danos por exposição ao sol sem protecção"],
    uv_details:["Ainda é noite, ou o dia está com céu muito nublado.",
                "Use óculos de sol em dias ensolarados; caso tenha uma pele particularmente sencível,<br>" +
                "ou exista neve no solo que reflete radiação UV, utilize protector solar.",
                "Utilize óculos de sol, e protector solar SPF 30+, proteja o corpo com roupa e use<br>" +
                "chapéu. Por volta do meio dia, quando o sol está mais intenso, procure estar à sombra.",
                "Utilize óculos de sol, e protector solar SPF 30+, proteja o corpo com roupas de protecção<br>" +
                "solar e use um chapéu com abas. Reduza a exposição ao sol nas duas horas anteriores e até às<br>" +
                "tres horas posteriores ao meio dia solar (cerca das 11:00 até às 16:00 durante o verão nas<br>" +
                "zonas que praticam horário de verão).",
                "Usar protetor solar FPS 30+, camisa, óculos de sol e um chapéu.<br>" +
                "Não fique ao sol por muito tempo.",
                "Tome todas as precauções, incluindo: Usar óculos de sol, protetor solar FPS 30+,<br>" +
                "cubra o corpo com roupa com mangas, vista calças, e use um chapéu de abas largas.<br>" +
                "Evite a exposição ao sol nas duas horas anteriores e até às tres horas posteriores ao meio dia solar<br>" +
                "(cerca das 11:00 até às 16:00 durante o verão nas zonas que praticam horário de verão)."],
    //
    solar_title:"Rad. Solar",
    solar_currentMax:"Valor máximo teórico actual",
    solar_ofMax:"do máximo",
    solar_maxToday:"Hoje, valor máximo",
    //
    cloudbase_title : "Base das Nuvens",
    cloudbase_popup_title : "Base das Nuvens Teórica",
    cloudbase_popup_text : "É um cálculo simples; 1000 pés por cada 4.4 graus Fahrenheit de diferença<br>" +
                           "entre a temperatura e o ponto de orvalho. Note que isto dá simplesmente<br>" +
                           "a altura teórica em que nuvens tipo Cumulus se começam a formar, e o<br>" +
                           "ar começa a ficar saturado",
    feet: "pés",
    metres: "metros",
    //
    lowest_info:"Mais Baixa",
    highest_info:"Máxima",
    lowestF_info:"Mínima",
    highestF_info:"Máxima",
    RisingVeryRapidly:"A subir muito rápidamente",
    RisingQuickly:"A subir rápidamente",
    Rising:"A subir",
    RisingSlowly:"A subir lentamente",
    Steady:"Estável",
    FallingSlowly:"A descer lentamente",
    Falling:"A descer",
    FallingQuickly:"A descer rápidamente",
    FallingVeryRapidly:"A descer muito rápidamente",
    //
    maximum_info:"Máximo",
    max_hour_info:"Máx. por Hora",
    minimum_info:"Mínimo",
    //
    coords:["N","NNE","NE","ENE","E","ESE","SE","SSE","S","OSO","O","OSO","O","ONO","NO","NNO","ONO"],
    compass:["N","NE","E","SE","S","SO","O","NO"],
    months:["Janeiro","Fevereiro","Março","Abril","Maio","Junho","Julho","Agosto","Setembro","Outubro","Novembro","Dezembro"]
};

//======================================================================================================================
// Czech by Milos Jirik
//======================================================================================================================
LANG.CS = {
    canvasnosupport : "Ve vašem prohlížeči není podpora pro HTML5 Canvas... Lituji...<br>" +
                      "Zkuste aktualizovat svůj prohlížeč na novější verzi - téměř všechny prohlížeče podporují nyní Canvas, i IE9!<br><br>" +
                      "Budete přesměrováni na původní stránku měřidel...",
    //
    led_title : "Senzor dálkového ovládání stav neznámý",
    led_title_ok : "Remote sensor OK",
    led_title_lost : "Senzor dálkového ovládání kontakt ztracen!",
    led_title_unknown : "Senzor dálkového ovládání stav neznámý!",
    led_title_offline: "Meteorologická stanice je nyní v režimu offline.",
    //
    weather   : "počasí",
    latitude  : "Zeměpisná délka",
    longitude : "Zeměpisná šířka",
    elevation : "Nadm. výška",
    //
    statusStr : "Nahrávání...",
    StatusMsg : "Stahování...",
    StatusHttp : "HTTP požadavek selhal",
    StatusRetry : "Opakování...",
    StatusRetryIn : "Opakování za...",
    StatusTimeout : "Vypršel časový limit",
    StatusPageLimit : "Automatická aktualizace stránky hotova, klepněte na stavovou LED pro pokračování",
    //
    StatusLastUpdate : "Poslední aktualizace před",
    StatusMinsAgo : "minutami",
    StatusHoursAgo : "hodinami",
    StatusDaysAgo : "dny",
    //
    realtimeCorrupt : "Stahovaný textový soubor poškozen! Opakování...",
    //
    timer : "sec.",
    at : "v",
    //
    // MAXIMUM number of characters that can be used for the 'title' variables (such as 'LANG_EN.temp_title_out')
    //        11 characters  ===="12345678901"====  11 characters
    //
    temp_title_out : "Teplota",
    temp_title_in : "Vn. teplota",
    temp_out_info : "Venkovní teplota",
    temp_out_web : "Venkovní",
    temp_in_info : "Vnitřní teplota",
    temp_in_web : "Vnitřní",
    temp_trend_info : "Teplotní trend",
    //
    dew_title : "Rosný bod",
    dew_info : "Rosný bod",
    dew_web : "Rosný bod",
    apptemp_title : "Zdánl. tepl.",
    apptemp_info : "Zdánlivá teplota",
    apptemp_web : "Zdánl. teplota",
    chill_title : "Chlad větru",
    chill_info : "Chlad větru",
    chill_web : "Chlad větru",
    heat_title : "Index horka",
    heat_info : "Index horka",
    heat_web : "Index horka",
    humdx_title : "Humidex",
    humdx_info : "Humidex",
    humdx_web : "Humidex",
    //
    rain_title : "Srážky",
    rrate_title : "Intenzita",
    rrate_info : "Intenzita srážek",
    LastRain_info : "Poslední srážky",
    LastRainedT_info : "Dnes v",
    LastRainedY_info : "Včera v",
    //
    hum_title_out : "Vlhkost",
    hum_title_in : "Vn. vlhkost",
    hum_out_info : "Venkovní vlhkost",
    hum_in_info : "Vnitřní vlhkost",
    hum_out_web : "Venkovní",
    hum_in_web : "Vnitřní",
    //
    baro_title : "Tlak vzd.",
    baro_info : "Barometrický tlak",
    baro_trend_info : "Tlaková tendence",
    //
    wind_title : "Vítr rychlost",
    tenminavg_title : "Prům. rychlost větru",
    tenminavgwind_info : "Prům. rychlost větru (10 min)",
    maxavgwind_info : "Max prům. rychlost větru",
    tenmingust_info : "Náraz (10 min)",
    maxgust_info : "Maximum náraz",
    latest_title : "Poslední vítr",
    latestwind_info : "Poslední rychlost větru",
    bearing_info : "Směr větru",
    latest_web : "Poslední",
    tenminavg_web : "Průměr",
    dominant_bearing : "Převažující směr větru dnes",
    calm: "bezvětří",
    windrose: "Větrná růžice",
    windruntoday: "Denní proběh větru",
    //
    uv_title : "UV Index",
    uv_levels : ["Žádné riziko",
                 "Nízké riziko",
                 "Střední riziko",
                 "Vysoké riziko",
                 "Velmi vysoké riziko",
                 "Extrémní riziko"],
    uv_headlines : ["Neměřitelný UV Index",
                    "Bez nebezpečí pro průměrné osoby",
                    "Riziko při nechráněném pobytu na slunci",
                    "Velké riziko při nechráněném pobytu na slunci",
                    "Vysoké riziko při nechráněném pobytu na slunci",
                    "Extreme riziko při nechráněném pobytu na slunci"],
    uv_details : ["Je ještě noc nebo je velmi zatažený den.",

                 "Noste sluneční brýle za jasných dnů, používejte opalovací krém, pokud je na zemi sníh,<br>" +
                 "který odráží UV záření, nebo máte-li zvlášť světlou pokožku.",

                 "Noste sluneční brýle a používejte krém na opalování SPF 30+, zakryjte tělo oblečením a<br>" +
                 "kloboukem, vyhledejte stín kolem poledne, kdy je slunce nejintenzivnější.",

                 "Noste sluneční brýle a používejte krém na opalování SPF 30+, zakryjte tělo ochranným<br>" +
                 "oděvem a kloboukem se širokou krempou, zkraťte dobu pobytu na slunci v době mezi 11:00 a 16:00 hod.,<br>" +
                 "tj. kolem slunečního poledne letního času na tři hodiny během léta v oblastech,<br>" +
                 "které používají letního času.",

                 "Používejte krém na opalování SPF 30+, košili, sluneční brýle a klobouk.<br>" +
                 "Nezůstávejte na slunci příliš dlouho.",

                 "Učiňte veškerá opatření, včetně nošení slunečních brýlí a použití krému na opalování SPF 30+,<br>" +
                 "zakryjte tělo košilí s dlouhým rukávem a kalhotami, noste velmi široký klobouk a<br>" +
                 "vyhněte se slunci od dvou hodin před až po tři hodiny po slunečním poledni (v době od 11:00<br>" +
                 "do 16:00 v létě v oblastech, které používají letního času)."],
    //
    solar_title : "Sluneční záření",
    solar_currentMax : "Aktuální teoretické maximum záření",
    solar_ofMax : "maximum",
    solar_maxToday : "Dnešní maximum záření",
    //
    lowest_info : "Nejnižší",
    highest_info : "Nejvyšší",
    lowestF_info : "Nejnižší",     // for proper translation of feminine words
    highestF_info : "Nejvyšší",    // for proper translation of feminine words
    //
    RisingVeryRapidly : "Prudký vzestup",
    RisingQuickly : "Silný vzestup",
    Rising : "Vzestup",
    RisingSlowly : "Slabý vzestup",
    Steady : "Ustálený stav",
    FallingSlowly : "Slabý pokles",
    Falling : "Pokles",
    FallingQuickly : "Silný pokles",
    FallingVeryRapidly : "Prudký pokles",
    //
    maximum_info : "Maximum",
    max_hour_info : "Max za hodinu",
    minimum_info : "Minimum",
    //
    coords : ["S", "SSV", "SV", "VSV", "V", "VJV", "JV", "JJV", "J", "JJZ", "JZ", "ZJZ", "Z", "ZSZ", "SZ", "SSZ"],
    compass : ["S", "SV", "V", "JV", "J", "JZ", "Z", "SZ"],
    months : ["Led", "Úno", "Bře", "Dub", "Kvě", "Čer", "Čec", "Srp", "Zář", "Říj", "Lis", "Pro"],
    //
    //
    cloudbase_title : "Základna mraků",
    cloudbase_popup_title : "Teoretická základna mraků",
    cloudbase_popup_text : "Výpočet je jednoduchý; 1000 ft na každých 4,4 stupňů Fahrenheita<br>" +
                           "rozdílu mezi teplotou a teplotou rosného bodu. Všimněte si, že to prostě<br>" +
                           "dává teoretickou výšku, ve které se kupovité mraky začínají tvořit, kdy<br>" +
                           "vzduch je nasycen",
};

//======================================================================================================================

function changeLang(newLang, updateGauges) {
    updateGauges = updateGauges === undefined ? true : updateGauges;
    // update the gauge titles etc
    if (updateGauges) {
        gauges.setLang(newLang);
    }

    // HTML boiler plate
    $('#lang_weather').html(newLang.weather);
    $('#lang_latitude').html(newLang.latitude);
    $('#lang_longitude').html(newLang.longitude);
    $('#lang_elevation').html(newLang.elevation);

    // update the web page radio buttons
    $('#lab_temp1').html(newLang.temp_out_web);
    $('#lab_temp2').html(newLang.temp_in_web);
    $('#lab_dew1').html(newLang.dew_web);
    $('#lab_dew2').html(newLang.apptemp_web);
    $('#lab_dew3').html(newLang.chill_web);
    $('#lab_dew4').html(newLang.heat_web);
    $('#lab_dew5').html(newLang.humdx_web);
    $('#lab_hum1').html(newLang.hum_out_web);
    $('#lab_hum2').html(newLang.hum_in_web);

    // unit table
    $('#lang_temperature').html(newLang.temp_title_out);
    $('#lang_rainfall').html(newLang.rain_title);
    $('#lang_pressure').html(newLang.baro_title);
    $('#lang_windSpeed').html(newLang.wind_title);
    $('#lang_cloudbase').html(newLang.cloudbase_title);
}
