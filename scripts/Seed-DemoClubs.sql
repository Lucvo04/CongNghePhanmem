-- Dữ liệu minh họa cho đồ án; chạy thủ công, không chạy khi ứng dụng khởi động.
-- Dùng sqlcmd -f 65001 -b -d StudentClubManagement -i scripts/Seed-DemoClubs.sql
-- Chỉ thêm mã CLB chưa có; không ghi đè CLB hoặc hồ sơ người dùng hiện có.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF DB_NAME() <> N'StudentClubManagement'
    THROW 50000, 'This script only targets the local StudentClubManagement project database.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'StudentClubManagement:SeedDemoClubs:v1',
        @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
    IF @LockResult < 0 THROW 50001, 'Could not acquire demo seed lock.', 1;

    DECLARE @ActorId nvarchar(450) = (
        SELECT u.Id FROM AspNetUsers u
        WHERE u.NormalizedEmail=N'LUCVOADMIN@CLB.LOCAL' AND u.Status=1
        AND EXISTS (SELECT 1 FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId
                    WHERE ur.UserId=u.Id AND r.NormalizedName=N'ADMIN')
    );
    IF @ActorId IS NULL THROW 50002, 'Expected active project administrator was not found.', 1;
    DECLARE @StudentRoleId nvarchar(450) = (SELECT Id FROM AspNetRoles WHERE NormalizedName=N'STUDENT');
    IF @StudentRoleId IS NULL THROW 50003, 'Student role is missing. Apply project migrations first.', 1;

    DECLARE @Samples TABLE (
        Code nvarchar(30) PRIMARY KEY, Name nvarchar(150), Category nvarchar(100),
        Description nvarchar(2000), ManagerLabel nvarchar(100), Alias varchar(20)
    );
    INSERT INTO @Samples VALUES
    (N'DEMO-CODE', N'Câu lạc bộ Lập trình', N'Học thuật',
     N'Không gian dành cho sinh viên yêu thích công nghệ, từ người mới bắt đầu đến những bạn đã có kinh nghiệm lập trình.' + NCHAR(10) + NCHAR(10) +
     N'Hoạt động định hướng: học C# và phát triển website, trao đổi thuật toán, làm dự án nhóm và chia sẻ kinh nghiệm xây dựng sản phẩm. Thành viên cùng luyện tư duy giải quyết vấn đề và kỹ năng làm việc nhóm.', N'Lập trình', 'code'),
    (N'DEMO-ENG', N'Câu lạc bộ Tiếng Anh', N'Ngoại ngữ',
     N'Cùng thực hành tiếng Anh trong một môi trường thân thiện, khuyến khích mỗi người tự tin giao tiếp và học hỏi từ bạn bè.' + NCHAR(10) + NCHAR(10) +
     N'Hoạt động định hướng: trò chuyện theo chủ đề, luyện thuyết trình, đọc sách và tìm hiểu văn hóa. Chào đón sinh viên ở nhiều trình độ, ưu tiên tinh thần chủ động và sẵn sàng chia sẻ.', N'Tiếng Anh', 'english'),
    (N'DEMO-MUSIC', N'Câu lạc bộ Âm nhạc', N'Nghệ thuật',
     N'Nơi gặp gỡ của những sinh viên yêu ca hát, nhạc cụ và sáng tạo âm nhạc. Bạn có thể tìm bạn đồng hành để luyện tập và khám phá phong cách riêng.' + NCHAR(10) + NCHAR(10) +
     N'Hoạt động định hướng: tập hát, giao lưu guitar và piano, luyện biểu diễn nhóm, xây dựng tiết mục cho các chương trình sinh viên. Không yêu cầu kinh nghiệm biểu diễn trước khi tìm hiểu CLB.', N'Âm nhạc', 'music'),
    (N'DEMO-PHOTO', N'Câu lạc bộ Nhiếp ảnh', N'Nghệ thuật',
     N'Ghi lại đời sống sinh viên qua những góc nhìn sáng tạo. Cùng khám phá cách kể một câu chuyện bằng hình ảnh và lưu giữ những khoảnh khắc đáng nhớ.' + NCHAR(10) + NCHAR(10) +
     N'Hoạt động định hướng: tìm hiểu bố cục, ánh sáng và chỉnh màu; thực hành chụp ảnh trong khuôn viên trường; góp ý bộ ảnh theo chủ đề. Có thể bắt đầu bằng điện thoại hoặc máy ảnh cá nhân.', N'Nhiếp ảnh', 'photo'),
    (N'DEMO-SPORT', N'Câu lạc bộ Cầu lông', N'Thể thao',
     N'Kết nối sinh viên yêu vận động thông qua cầu lông, cùng rèn luyện thể lực và tinh thần đồng đội trong môi trường tôn trọng, tích cực.' + NCHAR(10) + NCHAR(10) +
     N'Hoạt động định hướng: hướng dẫn kỹ thuật cơ bản, luyện tập theo nhóm trình độ và giao lưu thi đấu. Thành viên chú trọng khởi động, chơi đúng luật và hỗ trợ những bạn mới làm quen với bộ môn.', N'Cầu lông', 'badminton'),
    (N'DEMO-VOL', N'Câu lạc bộ Tình nguyện', N'Cộng đồng',
     N'Một cộng đồng sinh viên mong muốn đóng góp sức trẻ qua những việc làm thiết thực, lan tỏa sự sẻ chia và trách nhiệm với môi trường xung quanh.' + NCHAR(10) + NCHAR(10) +
     N'Hoạt động định hướng: hỗ trợ chương trình sinh viên, tổ chức hoạt động bảo vệ môi trường, xây dựng ý tưởng phục vụ cộng đồng và rèn luyện kỹ năng phối hợp khi tham gia tình nguyện.', N'Tình nguyện', 'volunteer');

    DECLARE @Code nvarchar(30), @Name nvarchar(150), @Category nvarchar(100), @Description nvarchar(2000),
        @ManagerLabel nvarchar(100), @Alias varchar(20), @UserId nvarchar(450), @Email nvarchar(256),
        @ClubId int, @MembershipId int, @Created int=0, @After nvarchar(2000);
    DECLARE samples CURSOR LOCAL FAST_FORWARD FOR SELECT Code,Name,Category,Description,ManagerLabel,Alias FROM @Samples ORDER BY Code;
    OPEN samples;
    FETCH NEXT FROM samples INTO @Code,@Name,@Category,@Description,@ManagerLabel,@Alias;
    WHILE @@FETCH_STATUS=0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM Clubs WITH (UPDLOCK,HOLDLOCK) WHERE Code=@Code)
        BEGIN
            SET @UserId=N'demo-club-manager-'+@Alias;
            SET @Email=N'demo.'+@Alias+N'@example.test';
            IF EXISTS (SELECT 1 FROM AspNetUsers WHERE Id=@UserId OR NormalizedEmail=UPPER(@Email)
                       OR NormalizedUserName=UPPER(@Email) OR StudentCode=@Code)
                THROW 50004, 'A demo manager identifier already exists. No existing account will be reused or modified.', 1;

            -- Không có PasswordHash, external login hoặc token: hồ sơ mẫu không có thông tin đăng nhập.
            INSERT INTO AspNetUsers (Id,UserName,NormalizedUserName,Email,NormalizedEmail,EmailConfirmed,
                PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnabled,AccessFailedCount,FullName,
                StudentCode,ClassName,Faculty,Status,CreatedAt,SecurityStamp,ConcurrencyStamp)
            VALUES (@UserId,@Email,UPPER(@Email),@Email,UPPER(@Email),0,0,0,1,0,
                N'Chủ nhiệm mẫu — '+@ManagerLabel,@Code,N'Dữ liệu mẫu',N'Dữ liệu mẫu',1,
                SYSDATETIMEOFFSET(),CONVERT(nvarchar(36),NEWID()),CONVERT(nvarchar(36),NEWID()));
            INSERT INTO AspNetUserRoles (UserId,RoleId) VALUES (@UserId,@StudentRoleId);
            INSERT INTO Clubs (Code,Name,Category,Description,Status,CreatedAt)
            VALUES (@Code,@Name,@Category,@Description+NCHAR(10)+NCHAR(10)+
                N'Lưu ý: Đây là CLB mẫu phục vụ đồ án và thử nghiệm tìm kiếm, không phải thông tin xác nhận về CLB chính thức của Trường Đại học Vinh.',1,SYSDATETIMEOFFSET());
            SET @ClubId=CONVERT(int,SCOPE_IDENTITY());
            INSERT INTO Memberships (ClubId,UserId,Role,Status,JoinedAt) VALUES (@ClubId,@UserId,2,1,SYSDATETIMEOFFSET());
            SET @MembershipId=CONVERT(int,SCOPE_IDENTITY());
            SET @After=(SELECT @Code AS Code,@MembershipId AS ManagerMembershipId,@UserId AS ManagerUserId,
                'Active' AS Status,CAST(1 AS bit) AS IsDemo FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
            INSERT INTO AuditLogs (ActorId,Action,EntityType,EntityId,AfterData,Reason,OccurredAt) VALUES
            (@ActorId,'StudentCreated','ApplicationUser',@UserId,N'{"Status":"Active","Role":"Student","IsDemo":true}',
                N'Tạo hồ sơ chủ nhiệm mẫu không có mật khẩu cho đồ án.',SYSDATETIMEOFFSET()),
            (@ActorId,'ClubCreated','Club',CONVERT(nvarchar(450),@ClubId),@After,
                N'Thêm CLB mẫu theo yêu cầu nhóm trưởng để thử tìm kiếm.',SYSDATETIMEOFFSET());
            SET @Created+=1;
        END;
        FETCH NEXT FROM samples INTO @Code,@Name,@Category,@Description,@ManagerLabel,@Alias;
    END;
    CLOSE samples;
    DEALLOCATE samples;
    COMMIT;
    SELECT @Created AS CreatedClubs, 6-@Created AS ExistingCodesSkipped;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;