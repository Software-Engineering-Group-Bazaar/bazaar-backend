using System.Text.Json.Serialization;

namespace Delivery.Navigation.Models
{
    public class GeoData
    {
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string? StreetAddress { get; set; }
    }
    public class Post
    {
        public GeoData To { get; set; }
        public GeoData From { get; set; }
    }

    public class GoogleGeocodingResponse
    {
        [JsonPropertyName("results")]
        public List<GeocodingResult> Results { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("error_message")] // For when status is not OK
        public string ErrorMessage { get; set; }
    }

    public class GeocodingResult
    {
        [JsonPropertyName("formatted_address")]
        public string FormattedAddress { get; set; }

        [JsonPropertyName("geometry")]
        public GeometryInfo Geometry { get; set; }

        [JsonPropertyName("place_id")]
        public string PlaceId { get; set; }
    }

    public class GeometryInfo
    {
        [JsonPropertyName("location")]
        public LocationInfo Location { get; set; }
    }

    public class LocationInfo
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lng")]
        public double Lng { get; set; }
    }

    public class DirectionsResponse
    {
        [JsonPropertyName("geocoded_waypoints")]
        public List<GeocodedWaypoint> GeocodedWaypoints { get; set; } = new List<GeocodedWaypoint>();

        [JsonPropertyName("routes")]
        public List<Route> Routes { get; set; } = new List<Route>();

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }
    }

    public class GeocodedWaypoint
    {
        [JsonPropertyName("geocoder_status")]
        public string GeocoderStatus { get; set; } = string.Empty;

        [JsonPropertyName("place_id")]
        public string PlaceId { get; set; } = string.Empty;

        [JsonPropertyName("types")]
        public List<string> Types { get; set; } = new List<string>();
    }

    public class Route
    {
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("legs")]
        public List<Leg> Legs { get; set; } = new List<Leg>();

        [JsonPropertyName("copyrights")]
        public string Copyrights { get; set; } = string.Empty;

        [JsonPropertyName("overview_polyline")]
        public Polyline? OverviewPolyline { get; set; } // Can be null

        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();

        [JsonPropertyName("waypoint_order")]
        public List<int> WaypointOrder { get; set; } = new List<int>();

        [JsonPropertyName("bounds")]
        public Bounds? Bounds { get; set; } // Can be null
    }

    public class Leg
    {
        [JsonPropertyName("steps")]
        public List<Step> Steps { get; set; } = new List<Step>();

        [JsonPropertyName("distance")]
        public Distance? Distance { get; set; } // Can be null

        [JsonPropertyName("duration")]
        public Duration? Duration { get; set; } // Can be null

        [JsonPropertyName("start_address")]
        public string StartAddress { get; set; } = string.Empty;

        [JsonPropertyName("end_address")]
        public string EndAddress { get; set; } = string.Empty;

        [JsonPropertyName("start_location")]
        public LocationPoint? StartLocation { get; set; } // Can be null

        [JsonPropertyName("end_location")]
        public LocationPoint? EndLocation { get; set; } // Can be null
    }

    public class Step
    {
        [JsonPropertyName("html_instructions")]
        public string HtmlInstructions { get; set; } = string.Empty;

        [JsonPropertyName("distance")]
        public Distance? Distance { get; set; } // Can be null

        [JsonPropertyName("duration")]
        public Duration? Duration { get; set; } // Can be null

        [JsonPropertyName("start_location")]
        public LocationPoint? StartLocation { get; set; } // Can be null

        [JsonPropertyName("end_location")]
        public LocationPoint? EndLocation { get; set; } // Can be null

        [JsonPropertyName("polyline")]
        public Polyline? Polyline { get; set; } // Can be null

        [JsonPropertyName("travel_mode")]
        public string TravelMode { get; set; } = string.Empty;

        [JsonPropertyName("maneuver")]
        public string? Maneuver { get; set; }
    }

    public class Distance
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public int Value { get; set; }
    }

    public class Duration
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public int Value { get; set; }
    }

    public class LocationPoint // Shared with Geocoding perhaps, ensure one definition
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lng")]
        public double Lng { get; set; }
    }

    public class Polyline
    {
        [JsonPropertyName("points")]
        public string Points { get; set; } = string.Empty;
    }

    public class Bounds
    {
        [JsonPropertyName("northeast")]
        public LocationPoint? Northeast { get; set; } // Can be null

        [JsonPropertyName("southwest")]
        public LocationPoint? Southwest { get; set; } // Can be null
    }

}