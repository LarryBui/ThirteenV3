module github.com/yourusername/tienlen-server

go 1.21

require (
	github.com/google/uuid v1.6.0
	github.com/heroiclabs/nakama-common v1.31.0
	google.golang.org/protobuf v1.31.0
)

replace github.com/yourusername/tienlen-server/pb => ./pb
