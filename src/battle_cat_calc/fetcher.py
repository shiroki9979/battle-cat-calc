import httpx
from .config import BASE_URL

class BCDataFetcher:
    """Handles network requests to fetch gacha data."""
    
    def __init__(self, timeout: int = 10):
        self.timeout = timeout

    async def fetch_html(self, seed: str, event: str, lang: str = "tw", count: int = 100) -> str:
        """Fetches the HTML content from the BC Godfat site."""
        params = {
            "seed": seed,
            "event": event,
            "lang": lang,
            "count": count
        }
        
        async with httpx.AsyncClient(timeout=self.timeout) as client:
            response = await client.get(BASE_URL, params=params)
            response.raise_for_status()
            return response.text
