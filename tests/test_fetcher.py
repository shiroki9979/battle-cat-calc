"""測試 BCDataFetcher 的網路請求功能。

動機：確保 Fetcher 能正確處理 HTTP 回應與錯誤，使用 Mock 隔離外部服務。
方法：使用 pytest-httpx 或 unittest.mock 模擬 HTTP 回應。
"""
import pytest
from unittest.mock import AsyncMock, patch, MagicMock
from battle_cat_calc.fetcher import BCDataFetcher


class TestBCDataFetcher:
    
    @pytest.mark.asyncio(loop_scope="function")
    async def test_fetch_html_success(self):
        """測試成功抓取 HTML。
        
        動機：驗證正常情況下 fetcher 能正確回傳 HTML 內容。
        方法：Mock httpx.AsyncClient.get 回傳模擬的 Response。
        """
        mock_response = MagicMock()
        mock_response.text = "<html><body><table></table></body></html>"
        mock_response.raise_for_status = MagicMock()
        
        with patch('httpx.AsyncClient') as mock_client:
            mock_instance = AsyncMock()
            mock_instance.get.return_value = mock_response
            mock_instance.__aenter__.return_value = mock_instance
            mock_instance.__aexit__.return_value = None
            mock_client.return_value = mock_instance
            
            fetcher = BCDataFetcher(timeout=5)
            result = await fetcher.fetch_html(
                seed="123456",
                event="test_event",
                lang="tw",
                count=10
            )
            
            assert result == "<html><body><table></table></body></html>"
            mock_instance.get.assert_called_once()

    @pytest.mark.asyncio(loop_scope="function")
    async def test_fetch_html_with_custom_params(self):
        """測試自訂參數是否正確傳遞。
        
        動機：確保所有參數都能正確傳遞給 HTTP 請求。
        方法：檢查 Mock 被呼叫時的參數。
        """
        mock_response = MagicMock()
        mock_response.text = "test"
        mock_response.raise_for_status = MagicMock()
        
        with patch('httpx.AsyncClient') as mock_client:
            mock_instance = AsyncMock()
            mock_instance.get.return_value = mock_response
            mock_instance.__aenter__.return_value = mock_instance
            mock_instance.__aexit__.return_value = None
            mock_client.return_value = mock_instance
            
            fetcher = BCDataFetcher()
            await fetcher.fetch_html(
                seed="999",
                event="special_event",
                lang="jp",
                count=200
            )
            
            # 驗證呼叫參數
            call_args = mock_instance.get.call_args
            params = call_args.kwargs.get('params', call_args[1].get('params', {}))
            
            assert params['seed'] == "999"
            assert params['event'] == "special_event"
            assert params['lang'] == "jp"
            assert params['count'] == 200

    def test_fetcher_default_timeout(self):
        """測試預設 timeout 值。
        
        動機：確保 timeout 有合理的預設值。
        方法：建立 Fetcher 並檢查 timeout 屬性。
        """
        fetcher = BCDataFetcher()
        assert fetcher.timeout == 10

    def test_fetcher_custom_timeout(self):
        """測試自訂 timeout 值。
        
        動機：確保 timeout 可被正確設定。
        方法：傳入自訂 timeout 並驗證。
        """
        fetcher = BCDataFetcher(timeout=30)
        assert fetcher.timeout == 30
