# Connect OpenAI billing

Saldo currently supports OpenAI Platform prepaid credit balances, using the unofficial `https://api.openai.com/v1/dashboard/billing/credit_grants` dashboard endpoint.

1. Open [OpenAI Billing](https://platform.openai.com/settings/organization/billing/overview) in your own browser. Sign in yourself and choose the correct organization.
2. Open browser developer tools (F12), choose **Network**, then reload Billing.
3. Find the request containing `credit_grants`. In **Request Headers**, locate **Authorization**.
4. Copy only the value beginning with `sess-`, without `Bearer `. Right-click Saldo → Settings and paste it into the masked session field. Do not paste it into chat or a GitHub issue.
5. Optionally enter your `org-...` organization ID. Click **Connect**. Saldo checks the connection before saving it.

If the request is absent, blocked, or uses a different authentication mechanism, the current integration may not work for your account. Do not disable browser protections or share your session to troubleshoot it. Check whether OpenAI has changed its billing interface.

An inference API key (`sk-...`), a ChatGPT subscription, and a browser billing session are different. Saldo does not create sessions, scrape login databases, intercept passwords or refresh credentials automatically.

The session is saved using Windows DPAPI for your Windows user in `%LOCALAPPDATA%/Saldo/session.dpapi`. Disconnect removes this local copy; it does not revoke the session at OpenAI. Anyone able to run code as your Windows user may still access it. Review account sessions on the provider's website when revocation is needed.

## 中文

在官网 Billing 登录并选对组织，按 F12 打开 Network，刷新后找到 `credit_grants` 请求。从请求头 Authorization 中复制 `sess-...` 部分，粘贴到 Saldo 设置中的密码框。会话不等于普通 API Key，不能分享给他人。若官网已改用不同的请求或授权方式，当前接入可能失效，不要关闭浏览器安全措施来强行连接。

Saldo 仅向固定的 OpenAI HTTPS 地址发送会话，禁止自动重定向。其他额度是总可用减去付费，并非接口单独报告的某一类赠送额度。登录过期后需重新连接。
