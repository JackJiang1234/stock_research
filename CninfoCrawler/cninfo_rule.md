
# cninfo 抓取规则描述

## 访问网址

https://www.cninfo.com.cn/new/disclosure/stock?orgId=gssh{{stock_code}}&stockCode={{stock_code}}

### 公告分类列表

| **分类名称**  | **参数值**                | **业务说明**    |
| --------- | ---------------------- | ----------- |
| 首次公开发行及上市 | category\_scgkfx\_szsh | IPO相关公告     |
| 年度报告      | category\_ndbg\_szsh   | 年度财务报告      |
| 半年度报告     | category\_bndbg\_szsh  | 中期财务报告      |
| 一季度报告     | category\_yjdbg\_szsh  | 第一季度报告      |
| 三季度报告     | category\_sjdbg\_szsh  | 第三季度报告      |
| 中介报告      | category\_zj\_szsh     | 中介机构出具的专业报告 |
| 股东大会      | category\_gddh\_szsh   | 股东大会相关公告    |
| 首发        | category\_sf\_szsh     | 首次发行相关      |
| 增发        | category\_zf\_szsh     | 再融资增发相关     |


### 查询某一分类的列表

| **http方法**  | **url**                | **FormData**    |
| --------- | ---------------------- | ----------- |
| post | https://www.cninfo.com.cn/new/hisAnnouncement/query | stock=600887%2Cgssh0600887&tabName=fulltext&pageSize=30&pageNum=1&column=sse&category=category_ndbg_szsh%3B&plate=sh&seDate=&searchkey=&secid=&sortName=&sortType=&isHLtitle=true |


### 最终下载 
GET        https://static.cninfo.com.cn/finalpage/2025-04-30/1223421131.PDF

