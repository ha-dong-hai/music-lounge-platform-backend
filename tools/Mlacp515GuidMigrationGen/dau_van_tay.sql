-- MLACP-515: dau van tay KHONG phu thuoc kieu id (so khop bang chuoi, collation CI) — chay truoc va sau migration roi so.
SET NOCOUNT ON;
DECLARE @r TABLE (k nvarchar(200), v bigint);
-- 1) so dong moi bang
DECLARE @s nvarchar(max) = N'';
SELECT @s += N'SELECT N''rows:' + t.name + N''', COUNT_BIG(*) FROM ' + QUOTENAME(t.name) + N' UNION ALL ' FROM sys.tables t WHERE t.schema_id = SCHEMA_ID('dbo') AND t.name <> '__EFMigrationsHistory';
SET @s = LEFT(@s, LEN(@s) - 10);
INSERT INTO @r EXEC (@s);
-- 2) moi FK mot cot: so dong co khoa ngoai VA tim thay dong cha
SET @s = N'';
SELECT @s += N'SELECT N''fk:' + OBJECT_NAME(fk.parent_object_id) + N'.' + pc.name + N'>' + OBJECT_NAME(fk.referenced_object_id) + N''', COUNT_BIG(*) FROM ' + QUOTENAME(OBJECT_NAME(fk.parent_object_id)) + N' c JOIN '
  + QUOTENAME(OBJECT_NAME(fk.referenced_object_id)) + N' p ON p.' + QUOTENAME(rc.name) + N' = c.' + QUOTENAME(pc.name) + N' UNION ALL '
FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = fk.object_id
JOIN sys.columns pc ON pc.object_id = fc.parent_object_id AND pc.column_id = fc.parent_column_id
JOIN sys.columns rc ON rc.object_id = fc.referenced_object_id AND rc.column_id = fc.referenced_column_id
WHERE fk.schema_id = SCHEMA_ID('dbo') AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id = fk.object_id) = 1;
SET @s = LEFT(@s, LEN(@s) - 10);
INSERT INTO @r EXEC (@s);
-- 3) tham chieu khong co FK: dem dong tro toi dong co that
INSERT INTO @r SELECT N'poly:complaints:show', COUNT_BIG(*) FROM complaints c JOIN lounge_shows s ON CONVERT(nvarchar(50), s.Id) = CONVERT(nvarchar(50), c.TargetId) WHERE c.TargetType = N'show';
INSERT INTO @r SELECT N'poly:emod:Show', COUNT_BIG(*) FROM event_moderations c JOIN lounge_shows s ON CONVERT(nvarchar(50), s.Id) = CONVERT(nvarchar(50), c.TargetId) WHERE c.TargetType = N'Show';
INSERT INTO @r SELECT N'poly:emod:Livestream', COUNT_BIG(*) FROM event_moderations c JOIN livestreams s ON CONVERT(nvarchar(50), s.Id) = CONVERT(nvarchar(50), c.TargetId) WHERE c.TargetType = N'Livestream';
INSERT INTO @r SELECT N'by:lounge_shows.CreatedBy', COUNT_BIG(*) FROM lounge_shows c JOIN users u ON CONVERT(nvarchar(50), u.Id) = CONVERT(nvarchar(50), c.CreatedBy);
INSERT INTO @r SELECT N'by:settlements.OwnerId', COUNT_BIG(*) FROM settlements c JOIN users u ON CONVERT(nvarchar(50), u.Id) = CONVERT(nvarchar(50), c.OwnerId);
INSERT INTO @r SELECT N'by:ledger_accounts.OwnerId', COUNT_BIG(*) FROM ledger_accounts c JOIN users u ON CONVERT(nvarchar(50), u.Id) = CONVERT(nvarchar(50), c.OwnerId);
INSERT INTO @r SELECT N'ref:notif:show', COUNT_BIG(*) FROM notifications n JOIN lounge_shows x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'show';
INSERT INTO @r SELECT N'ref:notif:livestream', COUNT_BIG(*) FROM notifications n JOIN lounge_shows x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'livestream';
INSERT INTO @r SELECT N'ref:notif:fnb_order', COUNT_BIG(*) FROM notifications n JOIN fnb_orders x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'fnb_order';
INSERT INTO @r SELECT N'ref:notif:event_moderation', COUNT_BIG(*) FROM notifications n JOIN event_moderations x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'event_moderation';
INSERT INTO @r SELECT N'ref:notif:kyc_review', COUNT_BIG(*) FROM notifications n JOIN users x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'kyc_review';
INSERT INTO @r SELECT N'ref:notif:lounge', COUNT_BIG(*) FROM notifications n JOIN music_lounges x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'lounge';
INSERT INTO @r SELECT N'ref:notif:ticket', COUNT_BIG(*) FROM notifications n JOIN tickets x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'ticket';
INSERT INTO @r SELECT N'ref:notif:subscription', COUNT_BIG(*) FROM notifications n JOIN owner_subscriptions x ON CONVERT(nvarchar(50), x.Id) = LEFT(n.ReferenceId, CHARINDEX(N':', n.ReferenceId + N':') - 1) WHERE n.ReferenceType = N'subscription';
INSERT INTO @r SELECT N'ref:pay:TicketHold', COUNT_BIG(*) FROM payments n JOIN ticket_holds x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'TicketHold';
INSERT INTO @r SELECT N'ref:pay:FnbOrder', COUNT_BIG(*) FROM payments n JOIN fnb_orders x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'FnbOrder';
INSERT INTO @r SELECT N'ref:pay:Subscription', COUNT_BIG(*) FROM payments n JOIN subscription_packages x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'Subscription';
INSERT INTO @r SELECT N'ref:pay:WalkIn', COUNT_BIG(*) FROM payments n JOIN lounge_shows x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'WalkIn';
INSERT INTO @r SELECT N'ref:ledger:payment', COUNT_BIG(*) FROM ledger_entries n JOIN payments x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'payment';
INSERT INTO @r SELECT N'ref:ledger:subscription', COUNT_BIG(*) FROM ledger_entries n JOIN subscription_packages x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'subscription';
-- Bo sung sau khi khao sat ban sao Azure 02/10: cac loai co tren du lieu that ma ban sao dev khong co
INSERT INTO @r SELECT N'poly:complaints:venue', COUNT_BIG(*) FROM complaints c JOIN music_lounges s ON CONVERT(nvarchar(50), s.Id) = CONVERT(nvarchar(50), c.TargetId) WHERE c.TargetType = N'venue';
INSERT INTO @r SELECT N'poly:crep:Show', COUNT_BIG(*) FROM content_reports c JOIN lounge_shows s ON CONVERT(nvarchar(50), s.Id) = CONVERT(nvarchar(50), c.TargetId) WHERE c.TargetType = N'Show';
INSERT INTO @r SELECT N'poly:crep:Livestream', COUNT_BIG(*) FROM content_reports c JOIN livestreams s ON CONVERT(nvarchar(50), s.Id) = CONVERT(nvarchar(50), c.TargetId) WHERE c.TargetType = N'Livestream';
INSERT INTO @r SELECT N'poly:emod:GalleryImage', COUNT_BIG(*) FROM event_moderations c JOIN lounge_gallery_images s ON CONVERT(nvarchar(50), s.Id) = CONVERT(nvarchar(50), c.TargetId) WHERE c.TargetType = N'GalleryImage';
INSERT INTO @r SELECT N'ref:notif:complaint', COUNT_BIG(*) FROM notifications n JOIN complaints x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'complaint';
INSERT INTO @r SELECT N'ref:notif:refund_request', COUNT_BIG(*) FROM notifications n JOIN refund_requests x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'refund_request';
INSERT INTO @r SELECT N'ref:notif:settlement', COUNT_BIG(*) FROM notifications n JOIN settlements x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'settlement';
INSERT INTO @r SELECT N'ref:notif:payout_owner', COUNT_BIG(*) FROM notifications n JOIN users x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'payout_owner';
INSERT INTO @r SELECT N'ref:notif:bank_account', COUNT_BIG(*) FROM notifications n JOIN bank_accounts x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'bank_account';
INSERT INTO @r SELECT N'ref:ledger:refund', COUNT_BIG(*) FROM ledger_entries n JOIN refund_requests x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'refund';
INSERT INTO @r SELECT N'ref:ledger:settlement', COUNT_BIG(*) FROM ledger_entries n JOIN settlements x ON CONVERT(nvarchar(50), x.Id) = n.ReferenceId WHERE n.ReferenceType = N'settlement';
-- 4) tien: tong so cai khong doi
INSERT INTO @r SELECT N'money:ledger_debit', CAST(SUM(CASE WHEN IsDebit = 1 THEN Amount ELSE 0 END) AS bigint) FROM ledger_entries;
INSERT INTO @r SELECT N'money:ledger_credit', CAST(SUM(CASE WHEN IsDebit = 0 THEN Amount ELSE 0 END) AS bigint) FROM ledger_entries;
SELECT k + N'=' + CAST(v AS nvarchar(30)) FROM @r ORDER BY k;
