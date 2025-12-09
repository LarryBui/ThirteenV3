FROM heroiclabs/nakama-pluginbuilder:3.21.1 AS builder

ENV GO111MODULE=on
ENV CGO_ENABLED=1

WORKDIR /backend

COPY go.mod .
COPY main.go .
COPY internal/ internal/
COPY pb/ pb/

RUN go mod tidy
RUN go build --trimpath --mod=mod --buildmode=plugin -o ./backend.so

FROM heroiclabs/nakama:3.21.1

COPY --from=builder /backend/backend.so /nakama/custom_modules/
