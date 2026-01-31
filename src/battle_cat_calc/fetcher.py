import httpx
from .config import BASE_URL

class BCDataFetcher:
    """處理抓取轉蛋資料的網路請求。"""
    
    def __init__(self, timeout: int = 10):
        self.timeout = timeout

    async def fetch_html(self, seed: str, event: str, lang: str = "tw", count: int = 100) -> str:
        """從 BC Godfat 網站抓取 HTML 內容。"""
        params = {
            "seed": seed,
            "event": event,
            "lang": lang,
            "count": count
        }
        
        async with httpx.AsyncClient(timeout=self.timeout, follow_redirects=True) as client:
            response = await client.get(BASE_URL, params=params)
            response.raise_for_status()
            return response.text
