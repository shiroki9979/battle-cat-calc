"""測試 BCDataFetcher 的錯誤處理路徑。

動機：原有測試僅覆蓋 happy path；HTTP 錯誤、逾時等 sad path 完全未測。
方法：使用 unittest.mock 模擬各種 httpx 異常情境。
"""
import pytest
import httpx
from unittest.mock import AsyncMock, patch, MagicMock
from battle_cat_calc.fetcher import BCDataFetcher


class TestFetcherErrorPaths:

    @pytest.mark.asyncio(loop_scope="function")
    async def test_http_status_error(self):
        """HTTP 4xx/5xx 應拋出 httpx.HTTPStatusError。"""
        mock_response = MagicMock()
        mock_response.raise_for_status.side_effect = httpx.HTTPStatusError(
            "Server Error",
            request=MagicMock(),
            response=MagicMock(status_code=500),
        )

        with patch('httpx.AsyncClient') as mock_client:
            mock_instance = AsyncMock()
            mock_instance.get.return_value = mock_response
            mock_instance.__aenter__.return_value = mock_instance
            mock_instance.__aexit__.return_value = None
            mock_client.return_value = mock_instance

            fetcher = BCDataFetcher()
            with pytest.raises(httpx.HTTPStatusError):
                await fetcher.fetch_html(seed="123", event="test")

    @pytest.mark.asyncio(loop_scope="function")
    async def test_connect_timeout(self):
        """連線逾時應拋出 httpx.ConnectTimeout。"""
        with patch('httpx.AsyncClient') as mock_client:
            mock_instance = AsyncMock()
            mock_instance.get.side_effect = httpx.ConnectTimeout("Timeout")
            mock_instance.__aenter__.return_value = mock_instance
            mock_instance.__aexit__.return_value = None
            mock_client.return_value = mock_instance

            fetcher = BCDataFetcher(timeout=1)
            with pytest.raises(httpx.ConnectTimeout):
                await fetcher.fetch_html(seed="123", event="test")

    @pytest.mark.asyncio(loop_scope="function")
    async def test_read_timeout(self):
        """讀取逾時應拋出 httpx.ReadTimeout。"""
        with patch('httpx.AsyncClient') as mock_client:
            mock_instance = AsyncMock()
            mock_instance.get.side_effect = httpx.ReadTimeout("Read timed out")
            mock_instance.__aenter__.return_value = mock_instance
            mock_instance.__aexit__.return_value = None
            mock_client.return_value = mock_instance

            fetcher = BCDataFetcher()
            with pytest.raises(httpx.ReadTimeout):
                await fetcher.fetch_html(seed="123", event="test")

    @pytest.mark.asyncio(loop_scope="function")
    async def test_network_error(self):
        """網路不通應拋出 httpx.ConnectError。"""
        with patch('httpx.AsyncClient') as mock_client:
            mock_instance = AsyncMock()
            mock_instance.get.side_effect = httpx.ConnectError("Connection refused")
            mock_instance.__aenter__.return_value = mock_instance
            mock_instance.__aexit__.return_value = None
            mock_client.return_value = mock_instance

            fetcher = BCDataFetcher()
            with pytest.raises(httpx.ConnectError):
                await fetcher.fetch_html(seed="123", event="test")

    @pytest.mark.asyncio(loop_scope="function")
    async def test_empty_response_body(self):
        """空回應 body 應回傳空字串（不應崩潰）。"""
        mock_response = MagicMock()
        mock_response.text = ""
        mock_response.raise_for_status = MagicMock()

        with patch('httpx.AsyncClient') as mock_client:
            mock_instance = AsyncMock()
            mock_instance.get.return_value = mock_response
            mock_instance.__aenter__.return_value = mock_instance
            mock_instance.__aexit__.return_value = None
            mock_client.return_value = mock_instance

            fetcher = BCDataFetcher()
            result = await fetcher.fetch_html(seed="123", event="test")
            assert result == ""

    @pytest.mark.asyncio(loop_scope="function")
    async def test_http_404(self):
        """HTTP 404 應拋出 HTTPStatusError。"""
        mock_response = MagicMock()
        mock_response.raise_for_status.side_effect = httpx.HTTPStatusError(
            "Not Found",
            request=MagicMock(),
            response=MagicMock(status_code=404),
        )

        with patch('httpx.AsyncClient') as mock_client:
            mock_instance = AsyncMock()
            mock_instance.get.return_value = mock_response
            mock_instance.__aenter__.return_value = mock_instance
            mock_instance.__aexit__.return_value = None
            mock_client.return_value = mock_instance

            fetcher = BCDataFetcher()
            with pytest.raises(httpx.HTTPStatusError):
                await fetcher.fetch_html(seed="123", event="bad_event")
